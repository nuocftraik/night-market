using NightMarket.WebApi.Application.Common.Exceptions;
using NightMarket.WebApi.Application.Identity.Functions;
using NightMarket.WebApi.Application.Identity.Roles;
using NightMarket.WebApi.Domain.Identity;
using NightMarket.WebApi.Infrastructure.Persistence.Context;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace NightMarket.WebApi.Infrastructure.Identity;

/// <summary>
/// Service xử lý function management operations
/// </summary>
internal class FunctionService : IFunctionService
{
    private readonly ApplicationDbContext _db;

    public FunctionService(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Get list tất cả functions với actions
    /// </summary>
    public async Task<List<FunctionDto>> GetListAsync(CancellationToken cancellationToken)
    {
        var functions = await _db.Functions
            .Include(f => f.ActionInFunctions)
            .ThenInclude(aif => aif.Action)
            .OrderBy(f => f.SortOrder)
            .ToListAsync(cancellationToken);

        return functions.Adapt<List<FunctionDto>>();
    }

    /// <summary>
    /// Get function details by ID với actions
    /// </summary>
    public async Task<FunctionDto> GetByIdAsync(string id)
    {
        var function = await _db.Functions
            .Include(f => f.ActionInFunctions)
            .ThenInclude(aif => aif.Action)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (function == null)
        {
            throw new NotFoundException("Function not found");
        }

        return function.Adapt<FunctionDto>();
    }

    /// <summary>
    /// Create hoặc update function với action assignments
    /// </summary>
    public async Task<string> CreateOrUpdateAsync(CreateOrUpdateFunctionRequest request)
    {
        if (string.IsNullOrEmpty(request.Id))
        {
            // Create new function
            var function = new Function 
            { 
                Id = request.Id ?? Guid.NewGuid().ToString(),
                Name = request.Name,
                Url = request.Url,
                SortOrder = request.SortOrder,
                ParentId = request.ParentId
            };
            
            if (string.IsNullOrEmpty(function.Id)) 
            {
                function.Id = Guid.NewGuid().ToString();
            }

            // Add actions to function
            if (request.ActionIds != null)
            {
                foreach (var actionId in request.ActionIds)
                {
                    function.AddAction(actionId);
                }
            }

            _db.Functions.Add(function);
            await _db.SaveChangesAsync();

            return function.Id;
        }
        else
        {
            // Update existing function
            var function = await _db.Functions
                .Include(f => f.ActionInFunctions)
                .FirstOrDefaultAsync(f => f.Id == request.Id);

            if (function == null)
            {
                throw new NotFoundException("Function not found");
            }

            // Update info
            function.Name = request.Name;
            function.Url = request.Url;
            function.SortOrder = request.SortOrder;
            function.ParentId = request.ParentId;

            // Update actions (replace all)
            function.UpdateActions(request.ActionIds);

            await _db.SaveChangesAsync();

            return function.Id;
        }
    }

    /// <summary>
    /// Delete function
    /// Cannot delete functions being used in Permission table
    /// </summary>
    public async Task<string> DeleteAsync(string id)
    {
        var function = await _db.Functions.FirstOrDefaultAsync(f => f.Id == id);

        if (function == null)
        {
            throw new NotFoundException("Function not found");
        }

        // Check if function is being used in Permission table
        var isUsedInPermissions = await _db.Permissions
            .AnyAsync(p => p.FunctionId == id);

        if (isUsedInPermissions)
        {
            throw new ConflictException(
                $"Cannot delete function '{function.Name}' as it is being used in permissions.");
        }

        // Remove related entries in ActionInFunction table to avoid constraint errors
        var actionInFunctions = await _db.ActionInFunctions.Where(x => x.FunctionId == id).ToListAsync();
        _db.ActionInFunctions.RemoveRange(actionInFunctions);

        _db.Functions.Remove(function);
        await _db.SaveChangesAsync();

        return id;
    }
}
