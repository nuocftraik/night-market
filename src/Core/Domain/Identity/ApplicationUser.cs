using Microsoft.AspNetCore.Identity;

namespace NightMarket.WebApi.Domain.Identity;

public class ApplicationUser : IdentityUser
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? ObjectId { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; }
}
