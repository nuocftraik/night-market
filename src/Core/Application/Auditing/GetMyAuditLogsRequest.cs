using NightMarket.WebApi.Application.Common.Models;

namespace NightMarket.WebApi.Application.Auditing;

/// <summary>
/// Request to get current user's audit logs with pagination and filters.
/// </summary>
public class GetMyAuditLogsRequest : PaginationFilter
{
    public string? TableName { get; set; }
    public string? Type { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
