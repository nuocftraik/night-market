namespace NightMarket.WebApi.Domain.Common.Contracts;

/// <summary>
/// Entity with audit trail (Created/Updated by/on)
/// </summary>
public abstract class AuditableEntity : BaseEntity
{
    public Guid CreatedBy { get; set; }
    public DateTime CreatedOn { get; private set; }

    public Guid LastModifiedBy { get; set; }
    public DateTime? LastModifiedOn { get; set; }

    protected AuditableEntity()
    {
        CreatedOn = DateTime.UtcNow;
        LastModifiedOn = DateTime.UtcNow;
    }
}

/// <summary>
/// Auditable entity with generic Id type
/// </summary>
public abstract class AuditableEntity<TId> : BaseEntity<TId>
{
    public Guid CreatedBy { get; set; }
    public DateTime CreatedOn { get; private set; }
    
    public Guid LastModifiedBy { get; set; }
    public DateTime? LastModifiedOn { get; set; }

    protected AuditableEntity()
    {
        CreatedOn = DateTime.UtcNow;
        LastModifiedOn = DateTime.UtcNow;
    }
}
