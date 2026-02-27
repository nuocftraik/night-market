using FluentValidation;

namespace NightMarket.WebApi.Application.Identity.Users;

/// <summary>
/// Request để update user profile
/// </summary>
public class UpdateUserRequest
{
    /// <summary>
    /// User ID (required)
    /// </summary>
    public string Id { get; set; } = default!;

    /// <summary>
    /// First name
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    /// Last name
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    /// Phone number
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Email address (unique)
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Delete current avatar image
    /// </summary>
    public bool DeleteCurrentImage { get; set; } = false;
}

/// <summary>
/// Validator cho UpdateUserRequest
/// </summary>
public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator(IUserService userService)
    {
        RuleFor(p => p.Id)
            .NotEmpty()
            .WithMessage("User ID is required.");

        RuleFor(p => p.FirstName)
            .NotEmpty()
            .WithMessage("First name is required.")
            .MaximumLength(75)
            .WithMessage("First name must not exceed 75 characters.");

        RuleFor(p => p.LastName)
            .NotEmpty()
            .WithMessage("Last name is required.")
            .MaximumLength(75)
            .WithMessage("Last name must not exceed 75 characters.");

        RuleFor(p => p.Email)
            .NotEmpty()
            .WithMessage("Email is required.")
            .EmailAddress()
            .WithMessage("Invalid Email Address.")
            .MustAsync(async (user, email, _) => !await userService.ExistsWithEmailAsync(email!, user.Id))
            .WithMessage((_, email) => $"Email {email} is already registered.");

        RuleFor(u => u.PhoneNumber)
            .Cascade(CascadeMode.Stop)
            .MustAsync(async (user, phone, _) => !await userService.ExistsWithPhoneNumberAsync(phone!, user.Id))
            .WithMessage((_, phone) => $"Phone number {phone} is already registered.")
            .Unless(u => string.IsNullOrWhiteSpace(u.PhoneNumber));
    }
}
