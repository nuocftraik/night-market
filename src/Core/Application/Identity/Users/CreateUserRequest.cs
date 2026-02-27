using FluentValidation;

namespace NightMarket.WebApi.Application.Identity.Users;

/// <summary>
/// Request để tạo user mới
/// </summary>
public class CreateUserRequest
{
    /// <summary>
    /// First name (required)
    /// </summary>
    public string FirstName { get; set; } = default!;

    /// <summary>
    /// Last name (required)
    /// </summary>
    public string LastName { get; set; } = default!;

    /// <summary>
    /// Email address (required, unique)
    /// </summary>
    public string Email { get; set; } = default!;

    /// <summary>
    /// Username (required, unique, min 6 chars)
    /// </summary>
    public string UserName { get; set; } = default!;

    /// <summary>
    /// Password (required, min 6 chars)
    /// </summary>
    public string Password { get; set; } = default!;

    /// <summary>
    /// Confirm password (must match Password)
    /// </summary>
    public string ConfirmPassword { get; set; } = default!;

    /// <summary>
    /// Phone number (optional, unique if provided)
    /// </summary>
    public string? PhoneNumber { get; set; }
}

/// <summary>
/// Validator cho CreateUserRequest
/// </summary>
public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator(IUserService userService)
    {
        RuleFor(u => u.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Email is required.")
            .EmailAddress()
            .WithMessage("Invalid Email Address.")
            .MustAsync(async (email, _) => !await userService.ExistsWithEmailAsync(email))
            .WithMessage((_, email) => $"Email {email} is already registered.");

        RuleFor(u => u.UserName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Username is required.")
            .MinimumLength(6)
            .WithMessage("Username must be at least 6 characters.")
            .MustAsync(async (name, _) => !await userService.ExistsWithNameAsync(name))
            .WithMessage((_, name) => $"Username {name} is already taken.");

        RuleFor(u => u.PhoneNumber)
            .Cascade(CascadeMode.Stop)
            .MustAsync(async (phone, _) => !await userService.ExistsWithPhoneNumberAsync(phone!))
            .WithMessage((_, phone) => $"Phone number {phone} is already registered.")
            .Unless(u => string.IsNullOrWhiteSpace(u.PhoneNumber));

        RuleFor(p => p.FirstName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("First name is required.");

        RuleFor(p => p.LastName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Last name is required.");

        RuleFor(p => p.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Password is required.")
            .MinimumLength(6)
            .WithMessage("Password must be at least 6 characters.");

        RuleFor(p => p.ConfirmPassword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Confirm password is required.")
            .Equal(p => p.Password)
            .WithMessage("Password and Confirm Password must match.");
    }
}
