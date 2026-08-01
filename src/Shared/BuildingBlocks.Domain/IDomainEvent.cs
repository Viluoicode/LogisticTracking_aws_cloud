namespace Logistics.BuildingBlocks.Domain;

/// <summary>Sự kiện xảy ra trong domain (nội bộ, chưa phải integration event).</summary>
public interface IDomainEvent
{
    DateTime OccurredOnUtc { get; }
}
