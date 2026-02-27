using NightMarket.WebApi.Domain.Common.Contracts;

namespace NightMarket.WebApi.Domain.Common.Events;

// Static class - chứa hàm factory tạo event
public static class EntityCreatedEvent
{
    // Factory method: Tạo event dễ dàng với type inference
    public static EntityCreatedEvent<TEntity> WithEntity<TEntity>(TEntity entity)
        where TEntity : IEntity
        => new(entity);
}

// Generic class - chứa thông tin entity được tạo
public class EntityCreatedEvent<TEntity> : DomainEvent
    where TEntity : IEntity
{
    // Entity vừa được tạo
    public TEntity Entity { get; }
    
    // Constructor internal - chỉ factory method mới tạo được
    internal EntityCreatedEvent(TEntity entity) => Entity = entity;
}
