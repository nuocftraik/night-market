using Microsoft.AspNetCore.Identity;

namespace NightMarket.WebApi.Domain.Identity;

public class ApplicationRole : IdentityRole
{
    public ApplicationRole()
    {
    }

    public ApplicationRole(string roleName, string description)
        : base(roleName)
    {
        Description = description;
    }

    public string? Description { get; set; }
}
