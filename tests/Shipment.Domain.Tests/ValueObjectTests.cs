using Logistics.Shipment.Domain;
using Xunit;

namespace Logistics.Shipment.Domain.Tests;

public class ValueObjectTests
{
    [Fact]
    public void Addresses_with_same_values_are_equal()
    {
        var a = new Address("1 A", "HCMC", "700000");
        var b = new Address("1 A", "HCMC", "700000");

        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void Addresses_with_different_values_are_not_equal()
    {
        var a = new Address("1 A", "HCMC", "700000");
        var b = new Address("2 B", "Hanoi", "100000");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void TrackingCode_roundtrips_via_From()
    {
        var code = TrackingCode.New();
        var same = TrackingCode.From(code.Value);

        Assert.Equal(code, same);
    }

    [Fact]
    public void TrackingCode_From_rejects_empty()
    {
        Assert.Throws<ArgumentException>(() => TrackingCode.From("  "));
    }
}
