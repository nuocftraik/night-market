using NightMarket.WebApi.Domain.Common.Contracts;

namespace NightMarket.WebApi.Domain.Common.Events;

public static class EntityUpdatedEvent
{
    public static EntityUpdatedEvent<TEntity> WithEntity<TEntity>(TEntity entity)
        where TEntity : IEntity
        => new(entity);
}

public class EntityUpdatedEvent<TEntity> : DomainEvent
    where TEntity : IEntity
{
    public TEntity Entity { get; }
    
    internal EntityUpdatedEvent(TEntity entity) => Entity = entity;
}
