using Logistics.BuildingBlocks.Domain;

namespace Logistics.Shipment.Domain;

/// <summary>Mã vận đơn — value object bọc string, có luật tạo/validate.</summary>
public sealed class TrackingCode : ValueObject
{
    public string Value { get; }

    private TrackingCode(string value) => Value = value;

    public static TrackingCode New()
        => new($"LT{DateTime.UtcNow:yyyyMMdd}{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}");

    public static TrackingCode From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Tracking code cannot be empty.", nameof(value));

        return new TrackingCode(value);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
