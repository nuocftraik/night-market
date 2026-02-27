namespace NightMarket.WebApi.Application.Identity.Users;

public class UserRoleDto
{
    public string? RoleId { get; set; }
    public string? RoleName { get; set; }
    public bool Enabled { get; set; }
}

public class UserRolesRequest
{
    public List<UserRoleDto> UserRoles { get; set; } = new();
}
