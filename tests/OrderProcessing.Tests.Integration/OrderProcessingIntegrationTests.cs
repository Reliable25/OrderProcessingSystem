using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using OrderProcessing.Application.DTOs;
using OrderProcessing.Infrastructure.Persistence;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace OrderProcessing.Tests.Integration;

public class OrderProcessingIntegrationTests : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        // Start the app using the appsettings configuration (DefaultConnection)
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // Force Development environment so Program.cs uses EnsureCreated() + seeding path
            builder.UseSetting("environment", "Development");
        });

        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // Wait until the in-process app has created and seeded the DB by checking AppDbContext directly.
        await WaitForSeedingAsync(TimeSpan.FromSeconds(60));
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Concurrency_NoOversell_WhenManyConcurrentOrders()
    {
        // deterministic seeded product id used by Program.cs
        var productId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var concurrency = 6;
        var quantityPerOrder = 30;

        var tasks = Enumerable.Range(0, concurrency).Select(async i =>
        {
            var req = new PlaceOrderRequest
            {
                CustomerEmail = $"user{i}@example.com",
                CustomerName = $"User {i}",
                Items = new List<OrderItemRequest> { new() { ProductId = productId, Quantity = quantityPerOrder } },
                IdempotencyKey = Guid.NewGuid().ToString()
            };

            var resp = await _client!.PostAsJsonAsync("/api/orders", req);
            return resp;
        }).ToArray();

        await Task.WhenAll(tasks);

        var successCount = tasks.Count(t => t.Result.IsSuccessStatusCode);

        // sanity: at least one succeeded and not everyone succeeded (controlled by seed stock)
        successCount.Should().BeGreaterThanOrEqualTo(1);
        successCount.Should().BeLessThanOrEqualTo(4);

        // Validate remaining stock via DB (more reliable than parsing HTTP response)
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId);
        product.Should().NotBeNull();
        product!.StockQuantity.Should().BeGreaterThanOrEqualTo(0);
    }

    private async Task WaitForSeedingAsync(TimeSpan timeout)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            try
            {
                using var scope = _factory!.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                if (await db.Products.AnyAsync())
                    return;
            }
            catch
            {
                // ignore and retry
            }

            await Task.Delay(500);
        }

        throw new TimeoutException("Timed out waiting for in-process app to seed products.");
    }
}