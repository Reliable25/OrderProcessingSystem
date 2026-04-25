using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using OrderProcessing.Application.Validators;

namespace OrderProcessing.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Register MediatR — scans this assembly for all IRequestHandler<,> implementations
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
        });

        // Register FluentValidation validators
        services.AddValidatorsFromAssemblyContaining<PlaceOrderRequestValidator>();

        return services;
    }
}