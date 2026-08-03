namespace Logistics.Shipment.Application.Abstractions;

/// <summary>Port: phát integration event ra ngoài (SNS). Infrastructure hiện thực.</summary>
public interface IEventPublisher
{
    Task PublishAsync(string messageType, string payload, string? traceParent, CancellationToken ct);
}
