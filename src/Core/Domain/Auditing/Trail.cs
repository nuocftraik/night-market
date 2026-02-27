using NightMarket.WebApi.Domain.Common.Contracts;

namespace NightMarket.WebApi.Domain.Auditing;

/// <summary>
/// Audit trail entity — lưu trữ tất cả thay đổi trong hệ thống.
/// Inherits BaseEntity (NOT AuditableEntity) to avoid infinite audit loop.
/// </summary>
public class Trail : BaseEntity, IAggregateRoot
{
    public Guid UserId { get; set; }
    public TrailType Type { get; set; }
    public string TableName { get; set; } = default!;
    public DateTime DateTime { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? AffectedColumns { get; set; }
    public string PrimaryKey { get; set; } = default!;

    public Trail()
    {
        DateTime = DateTime.UtcNow;
    }
}
