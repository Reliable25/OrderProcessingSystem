using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OrderProcessing.Application.Commands;
using OrderProcessing.Application.DTOs;
using OrderProcessing.Application.EventHandlers;
using OrderProcessing.Application.Validators;
using OrderProcessing.Domain.Interfaces;
using OrderProcessing.Infrastructure.HostedServices;
using OrderProcessing.Infrastructure.Persistence;
using OrderProcessing.Infrastructure.Repositories;
using OrderProcessing.Infrastructure.Service;
using Polly;
using Serilog;
using Serilog.Events;
using System.Reflection;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    Log.Information("Starting up");

    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog for the host
    builder.Host.UseSerilog();

    // Add services
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }
    });
    builder.Services.AddLogging();

    // --- DbContext: SQL Server connection
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? builder.Configuration["ConnectionStrings:DefaultConnection"];

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(connectionString));

    // Repos / UnitOfWork / application services
    builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
    builder.Services.AddScoped<IIdempotencyRepository, IdempotencyRepository>();
    builder.Services.AddScoped<IProductRepository, ProductRepository>();
    builder.Services.AddScoped<IOrderRepository, OrderRepository>();

    // Application handlers / services
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<PlaceOrderCommand>());
    builder.Services.AddScoped<OrderPlacedEventHandler>();

    // Register concrete (non-resilient) payment service implementation
    builder.Services.AddScoped<PaymentService>();
    // Register notification service
    builder.Services.AddScoped<INotificationService, NotificationService>();

    // --- Polly policies for payments ---
    // Retry on false results or exceptions with exponential backoff (3 retries).
    var retryPolicy = Policy<bool>
        .HandleResult(r => r == false)
        .Or<Exception>()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
            onRetry: (outcome, timespan, retryAttempt, context) =>
            {
                Log.Warning("Payment retry {Retry} after {Delay} due to {Reason}", retryAttempt, timespan, outcome.Exception?.Message ?? $"false-result");
            });

    // Circuit breaker: break on 5 consecutive failures for 30 seconds
    var circuitBreaker = Policy<bool>
        .HandleResult(r => r == false)
        .Or<Exception>()
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 5,
            durationOfBreak: TimeSpan.FromSeconds(30),
            onBreak: (outcome, breakDelay) =>
            {
                Log.Warning("Payment circuit broken for {Delay} due to {Reason}", breakDelay, outcome.Exception?.Message ?? "false-result");
            },
            onReset: () => Log.Information("Payment circuit reset"),
            onHalfOpen: () => Log.Information("Payment circuit half-open"));

    // Timeout: ensure payment attempts don't hang
    var timeout = Policy.TimeoutAsync<bool>(TimeSpan.FromSeconds(10));

    // Combine policies: timeout wraps inner, then retry/circuit-breaker
    var paymentPolicy = Policy.WrapAsync(retryPolicy, circuitBreaker, timeout);

    builder.Services.AddSingleton<IAsyncPolicy<bool>>(paymentPolicy);

    // Register resilient decorator for IPaymentService that uses the policy + concrete PaymentService
    builder.Services.AddScoped<IPaymentService>(sp =>
        new ResilientPaymentService(
            sp.GetRequiredService<PaymentService>(),
            sp.GetRequiredService<IAsyncPolicy<bool>>(),
            sp.GetRequiredService<ILogger<ResilientPaymentService>>()));

    // FluentValidation
    builder.Services.AddScoped<IValidator<PlaceOrderRequest>, PlaceOrderRequestValidator>();

    // Background cleanup
    builder.Services.AddHostedService<IdempotencyCleanupService>();

    var app = builder.Build();

    // Ensure DB and deterministic seed + logging of seeded IDs
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Using connection string: {Conn}", connectionString);

        try
        {
            var db = services.GetRequiredService<AppDbContext>();
            db.Database.Migrate();

            var unitOfWork = services.GetRequiredService<IUnitOfWork>();
            unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var prods = (await unitOfWork.Products.GetAllActiveAsync()).ToList();
                if (!prods.Any())
                {
                    var p1 = new OrderProcessing.Domain.Entities.Product("Widget A", "Basic widget", 9.99m, 100);
                    var p2 = new OrderProcessing.Domain.Entities.Product("Widget B", "Advanced widget", 19.99m, 50);

                    // set deterministic ids
                    typeof(OrderProcessing.Domain.Entities.Product)
                        .GetProperty("Id", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?
                        .SetValue(p1, Guid.Parse("11111111-1111-1111-1111-111111111111"));

                    typeof(OrderProcessing.Domain.Entities.Product)
                        .GetProperty("Id", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?
                        .SetValue(p2, Guid.Parse("22222222-2222-2222-2222-222222222222"));

                    await unitOfWork.Products.AddAsync(p1);
                    await unitOfWork.Products.AddAsync(p2);
                }
            }).GetAwaiter().GetResult();

            var seeded = (await services.GetRequiredService<IUnitOfWork>().Products.GetAllActiveAsync()).ToList();
            foreach (var p in seeded)
            {
                logger.LogInformation("Seeded product -> Id: {Id}, Name: {Name}, Stock: {Stock}", p.Id, p.Name, p.StockQuantity);
            }
        }
        catch (Exception ex)
        {
            var logger2 = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger2.LogError(ex, "An error occurred while creating or seeding the database.");
            throw;
        }
    }

        app.UseSwagger();
        app.UseSwaggerUI();

    app.UseHttpsRedirection();
    app.MapControllers();
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }