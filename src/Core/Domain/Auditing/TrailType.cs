namespace NightMarket.WebApi.Domain.Auditing;

/// <summary>
/// Type of audit trail entry.
/// </summary>
public enum TrailType : byte
{
    Create = 1,
    Update = 2,
    Delete = 3
}
