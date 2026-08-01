using Logistics.Shipment.Domain;
using Xunit;

namespace Logistics.Shipment.Domain.Tests;

public class ShipmentStateMachineTests
{
    private static Shipment NewShipment() =>
        Shipment.Create(
            new Address("1 Origin St", "HCMC", "700000"),
            new Address("2 Dest Ave", "Hanoi", "100000"));

    [Fact]
    public void Create_sets_Created_with_history_and_event()
    {
        var s = NewShipment();

        Assert.Equal(ShipmentStatus.Created, s.Status);
        Assert.Single(s.History);
        Assert.StartsWith("LT", s.Code.Value);
        Assert.Single(s.DomainEvents);
    }

    [Fact]
    public void Happy_path_transitions_succeed()
    {
        var s = NewShipment();

        s.MarkPickedUp();
        s.MarkInTransit();
        s.MarkOutForDelivery();
        s.MarkDelivered();

        Assert.Equal(ShipmentStatus.Delivered, s.Status);
        Assert.Equal(5, s.History.Count); // Created + 4 lần chuyển
    }

    [Fact]
    public void Invalid_transition_throws()
    {
        var s = NewShipment();

        // Created -> Delivered không hợp lệ
        Assert.Throws<InvalidShipmentTransitionException>(() => s.MarkDelivered());
    }

    [Fact]
    public void Delivered_is_terminal()
    {
        var s = NewShipment();
        s.MarkPickedUp();
        s.MarkInTransit();
        s.MarkOutForDelivery();
        s.MarkDelivered();

        Assert.Throws<InvalidShipmentTransitionException>(() => s.MarkReturned());
    }

    [Fact]
    public void Failed_can_be_Returned()
    {
        var s = NewShipment();

        s.MarkFailed();
        s.MarkReturned();

        Assert.Equal(ShipmentStatus.Returned, s.Status);
    }
}
