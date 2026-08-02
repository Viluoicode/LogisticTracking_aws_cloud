using System.Text.Json;
using Logistics.Shared.Contracts;
using Xunit;

namespace Logistics.Shipment.Integration.Tests;

/// <summary>
/// Contract test: khẳng định schema "trên đường dây" của integration event ổn định.
/// Nếu ai đổi tên field -> test fail -> tránh phá vỡ consumer (Tracking/Notification).
/// Không cần DB (không dùng fixture).
/// </summary>
public class IntegrationEventContractTests
{
    [Fact]
    public void ShipmentStatusChanged_has_expected_wire_fields()
    {
        var evt = new ShipmentStatusChangedIntegrationEvent(Guid.NewGuid(), "LT123", "Created", DateTime.UtcNow);
        var json = JsonSerializer.Serialize(evt); // giống publisher (Outbox)

        foreach (var field in new[] { "ShipmentId", "TrackingCode", "Status", "OccurredOnUtc" })
            Assert.Contains($"\"{field}\"", json);
    }

    [Fact]
    public void Producer_json_roundtrips_to_consumer_type()
    {
        var original = new ShipmentStatusChangedIntegrationEvent(Guid.NewGuid(), "LT123", "PickedUp", DateTime.UtcNow);
        var json = JsonSerializer.Serialize(original);                                       // Shipment publish
        var back = JsonSerializer.Deserialize<ShipmentStatusChangedIntegrationEvent>(json);  // consumer đọc

        Assert.Equal(original, back);
    }
}
