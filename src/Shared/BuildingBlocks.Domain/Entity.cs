namespace Logistics.BuildingBlocks.Domain;

/// <summary>Base cho mọi entity: có Id + gom domain events.</summary>
public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public Guid Id { get; protected set; } = Guid.NewGuid();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}

/// <summary>Đánh dấu gốc của một aggregate (ranh giới nhất quán).</summary>
public abstract class AggregateRoot : Entity;
