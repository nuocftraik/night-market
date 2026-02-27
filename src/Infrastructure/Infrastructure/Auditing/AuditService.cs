using NightMarket.WebApi.Application.Auditing;
using NightMarket.WebApi.Application.Common.Interfaces;
using NightMarket.WebApi.Application.Common.Models;
using NightMarket.WebApi.Domain.Auditing;
using NightMarket.WebApi.Infrastructure.Persistence.Context;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace NightMarket.WebApi.Infrastructure.Auditing;

/// <summary>
/// Service implementation for querying audit trails.
/// </summary>
internal class AuditService : IAuditService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;

    public AuditService(
        ApplicationDbContext context,
        ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PaginationResponse<AuditDto>> GetMyAuditLogsAsync(
        GetMyAuditLogsRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetUserId();

        var query = _context.AuditTrails
            .Where(a => a.UserId == userId)
            .AsQueryable();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(request.TableName))
            query = query.Where(a => a.TableName == request.TableName);

        if (!string.IsNullOrWhiteSpace(request.Type) &&
            Enum.TryParse<TrailType>(request.Type, out var trailType))
            query = query.Where(a => a.Type == trailType);

        if (request.FromDate.HasValue)
            query = query.Where(a => a.DateTime >= request.FromDate.Value);

        if (request.ToDate.HasValue)
            query = query.Where(a => a.DateTime <= request.ToDate.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var auditLogs = await query
            .OrderByDescending(a => a.DateTime)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = auditLogs.Select(t => new AuditDto
        {
            Id = t.Id,
            UserId = t.UserId,
            Type = t.Type.ToString(),
            TableName = t.TableName,
            DateTime = t.DateTime,
            OldValues = t.OldValues,
            NewValues = t.NewValues,
            AffectedColumns = t.AffectedColumns,
            PrimaryKey = t.PrimaryKey
        }).ToList();

        return new PaginationResponse<AuditDto>(
            dtos,
            totalCount,
            request.PageNumber,
            request.PageSize);
    }
}
