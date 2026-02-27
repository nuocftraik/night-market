using NightMarket.WebApi.Application.Identity.Functions;
using NightMarket.WebApi.Application.Identity.Roles;
using Microsoft.AspNetCore.Mvc;

namespace NightMarket.WebApi.Host.Controllers.Identity;

/// <summary>
/// Role management APIs
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RoleController : ControllerBase
{
    private readonly IRoleService _roleService;
    private readonly IFunctionService _functionService;

    public RoleController(IRoleService roleService, IFunctionService functionService)
    {
        _roleService = roleService;
        _functionService = functionService;
    }

    /// <summary>
    /// Get list of all roles
    /// </summary>
    [HttpGet]
    public Task<List<RoleDto>> GetListAsync(CancellationToken cancellationToken)
    {
        return _roleService.GetListAsync(cancellationToken);
    }

    /// <summary>
    /// Get role details by ID
    /// </summary>
    [HttpGet("{id}")]
    public Task<RoleDto> GetByIdAsync(string id)
    {
        return _roleService.GetByIdAsync(id);
    }

    /// <summary>
    /// Get role details với permissions (for permission UI)
    /// Returns list of Functions with Actions marked as Selected or not
    /// </summary>
    [HttpGet("{id}/permissions")]
    public Task<List<FunctionDto>> GetByIdWithPermissionsAsync(
        string id, 
        CancellationToken cancellationToken)
    {
        return _roleService.GetByIdWithPermissionsAsync(id, cancellationToken);
    }

    /// <summary>
    /// Update role's permissions (table-based approach)
    /// </summary>
    [HttpPut("{id}/permissions")]
    public async Task<ActionResult> UpdatePermissionsAsync(
        string id, 
        [FromBody] UpdateRolePermissionsRequest request, 
        CancellationToken cancellationToken)
    {
        if (id != request.RoleId)
        {
            return BadRequest();
        }

        var result = await _roleService.UpdatePermissionsAsync(request, cancellationToken);
        return Ok(new { message = result });
    }

    /// <summary>
    /// Create hoặc update role
    /// </summary>
    [HttpPost("create/update")]
    public async Task<ActionResult> RegisterRoleAsync([FromBody] CreateOrUpdateRoleRequest request)
    {
        var result = await _roleService.CreateOrUpdateAsync(request);
        return Ok(new { message = result });
    }

    /// <summary>
    /// Delete role
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteAsync(string id)
    {
        var result = await _roleService.DeleteAsync(id);
        return Ok(new { message = result });
    }

    /// <summary>
    /// Get list of all functions (for permission UI)
    /// </summary>
    [HttpGet("functions")]
    public Task<List<FunctionDto>> GetFunctionListAsync(CancellationToken cancellationToken)
    {
        return _functionService.GetListAsync(cancellationToken);
    }

    /// <summary>
    /// Get function details by ID
    /// </summary>
    [HttpGet("function/{id}")]
    public Task<FunctionDto> GetFunctionByIdAsync(string id)
    {
        return _functionService.GetByIdAsync(id);
    }

    /// <summary>
    /// Create hoặc update function
    /// </summary>
    [HttpPost("function/create/update")]
    public async Task<ActionResult> CreateUpdateFunctionAsync([FromBody] CreateOrUpdateFunctionRequest request)
    {
        var result = await _functionService.CreateOrUpdateAsync(request);
        return Ok(new { message = result });
    }

    /// <summary>
    /// Delete function
    /// </summary>
    [HttpDelete("function/{id}")]
    public async Task<ActionResult> DeleteFunctionAsync(string id)
    {
        var result = await _functionService.DeleteAsync(id);
        return Ok(new { message = result });
    }
}
