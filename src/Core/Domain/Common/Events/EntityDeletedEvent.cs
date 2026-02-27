using NightMarket.WebApi.Domain.Common.Contracts;

namespace NightMarket.WebApi.Domain.Common.Events;

public static class EntityDeletedEvent
{
    public static EntityDeletedEvent<TEntity> WithEntity<TEntity>(TEntity entity)
        where TEntity : IEntity
        => new(entity);
}

public class EntityDeletedEvent<TEntity> : DomainEvent
    where TEntity : IEntity
{
    public TEntity Entity { get; }
    
    internal EntityDeletedEvent(TEntity entity) => Entity = entity;
}
