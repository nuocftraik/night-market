namespace NightMarket.WebApi.Domain.Common.Contracts;

/// <summary>
/// Entity with audit trail (Created/Updated by/on) + Soft Delete
/// </summary>
public abstract class AuditableEntity : BaseEntity, ISoftDelete
{
    public Guid CreatedBy { get; set; }
    public DateTime CreatedOn { get; private set; }

    public Guid LastModifiedBy { get; set; }
    public DateTime? LastModifiedOn { get; set; }

    // ISoftDelete
    public DateTime? DeletedOn { get; set; }
    public Guid? DeletedBy { get; set; }

    protected AuditableEntity()
    {
        CreatedOn = DateTime.UtcNow;
        LastModifiedOn = DateTime.UtcNow;
    }
}

/// <summary>
/// Auditable entity with generic Id type + Soft Delete
/// </summary>
public abstract class AuditableEntity<TId> : BaseEntity<TId>, ISoftDelete
{
    public Guid CreatedBy { get; set; }
    public DateTime CreatedOn { get; private set; }
    
    public Guid LastModifiedBy { get; set; }
    public DateTime? LastModifiedOn { get; set; }

    // ISoftDelete
    public DateTime? DeletedOn { get; set; }
    public Guid? DeletedBy { get; set; }

    protected AuditableEntity()
    {
        CreatedOn = DateTime.UtcNow;
        LastModifiedOn = DateTime.UtcNow;
    }
}
