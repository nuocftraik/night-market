using Microsoft.AspNetCore.Identity;

namespace NightMarket.WebApi.Domain.Identity;

/// <summary>
/// Custom role claim entity.
/// </summary>
public class ApplicationRoleClaim : IdentityRoleClaim<string>
{
    public string? CreatedBy { get; init; }
    public DateTime CreatedOn { get; init; }
}
