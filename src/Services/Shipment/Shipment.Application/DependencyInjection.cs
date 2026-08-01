using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Logistics.Shipment.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddShipmentApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        return services;
    }
}
