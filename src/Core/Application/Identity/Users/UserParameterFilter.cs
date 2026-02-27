using NightMarket.WebApi.Application.Common.Models;

namespace NightMarket.WebApi.Application.Identity.Users;

/// <summary>
/// Filter cho user search với pagination
/// </summary>
public class UserParameterFilter : PaginationFilter
{
    /// <summary>
    /// Filter by active status
    /// </summary>
    public bool? IsActive { get; set; }

    /// <summary>
    /// Filter by email confirmed status
    /// </summary>
    public bool? EmailConfirmed { get; set; }
}
