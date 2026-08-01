using Logistics.BuildingBlocks.Domain;

namespace Logistics.Shipment.Domain;

/// <summary>
/// Aggregate root — trái tim nghiệp vụ. Trạng thái CHỈ đổi qua các method Mark*,
/// và mọi chuyển tiếp phải hợp lệ theo state machine. Không setter public.
/// </summary>
public sealed class Shipment : AggregateRoot
{
    private static readonly IReadOnlyDictionary<ShipmentStatus, ShipmentStatus[]> AllowedTransitions =
        new Dictionary<ShipmentStatus, ShipmentStatus[]>
        {
            [ShipmentStatus.Created]        = [ShipmentStatus.PickedUp, ShipmentStatus.Failed],
            [ShipmentStatus.PickedUp]       = [ShipmentStatus.InTransit, ShipmentStatus.Failed],
            [ShipmentStatus.InTransit]      = [ShipmentStatus.OutForDelivery, ShipmentStatus.Failed],
            [ShipmentStatus.OutForDelivery] = [ShipmentStatus.Delivered, ShipmentStatus.Failed],
            [ShipmentStatus.Failed]         = [ShipmentStatus.Returned],
            [ShipmentStatus.Delivered]      = [],
            [ShipmentStatus.Returned]       = []
        };

    private readonly List<StatusChange> _history = new();

    public TrackingCode Code { get; private set; } = null!;
    public Address Origin { get; private set; } = null!;
    public Address Destination { get; private set; } = null!;
    public ShipmentStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<StatusChange> History => _history.AsReadOnly();

    private Shipment() { } // EF materialization

    public static Shipment Create(Address origin, Address destination)
    {
        var shipment = new Shipment
        {
            Code = TrackingCode.New(),
            Origin = origin,
            Destination = destination,
            Status = ShipmentStatus.Created,
            CreatedAtUtc = DateTime.UtcNow
        };

        shipment._history.Add(new StatusChange(ShipmentStatus.Created, shipment.CreatedAtUtc));
        shipment.Raise(new ShipmentStatusChangedDomainEvent(
            shipment.Id, shipment.Code.Value, ShipmentStatus.Created));

        return shipment;
    }

    public void MarkPickedUp()       => Transition(ShipmentStatus.PickedUp);
    public void MarkInTransit()      => Transition(ShipmentStatus.InTransit);
    public void MarkOutForDelivery() => Transition(ShipmentStatus.OutForDelivery);
    public void MarkDelivered()      => Transition(ShipmentStatus.Delivered);
    public void MarkFailed()         => Transition(ShipmentStatus.Failed);
    public void MarkReturned()       => Transition(ShipmentStatus.Returned);

    private void Transition(ShipmentStatus next)
    {
        if (!AllowedTransitions[Status].Contains(next))
            throw new InvalidShipmentTransitionException(Status, next);

        Status = next;
        _history.Add(new StatusChange(next, DateTime.UtcNow));
        Raise(new ShipmentStatusChangedDomainEvent(Id, Code.Value, next));
    }
}
