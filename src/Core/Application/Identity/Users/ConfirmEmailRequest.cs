namespace NightMarket.WebApi.Application.Identity.Users;

public class ConfirmEmailRequest
{
    public string UserId { get; set; } = default!;
    public string Code { get; set; } = default!;
    public string? Tenant { get; set; }
}
