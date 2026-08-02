using FluentValidation;
using Logistics.Shipment.Application.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Logistics.Shipment.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddShipmentApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>)); // validate trước mọi handler
        });
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        return services;
    }
}
