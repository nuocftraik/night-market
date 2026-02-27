using NightMarket.WebApi.Application.Common.Interfaces;
using NightMarket.WebApi.Application.Common.Models;

namespace NightMarket.WebApi.Application.Auditing;

/// <summary>
/// Service interface for querying audit trails.
/// </summary>
public interface IAuditService : ITransientService
{
    Task<PaginationResponse<AuditDto>> GetMyAuditLogsAsync(
        GetMyAuditLogsRequest request,
        CancellationToken cancellationToken = default);
}
