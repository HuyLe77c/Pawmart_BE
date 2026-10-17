using System.ComponentModel.DataAnnotations;

namespace Pawmart.Models.Contracts.Auth;

public sealed class RegisterRequest
{
    [Required, StringLength(150), EmailAddress]
    public string Email { get; init; } = "";

    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; init; } = "";

    [Required, StringLength(128, MinimumLength = 8)]
    public string Password { get; init; } = "";

    [StringLength(20)]
    public string? PhoneNumber { get; init; }
}

public sealed class LoginRequest
{
    [Required, StringLength(150), EmailAddress]
    public string Email { get; init; } = "";

    [Required, StringLength(128)]
    public string Password { get; init; } = "";
}

public sealed record UserResponse(string UserId, string Email, string FullName, string? PhoneNumber, string Role);

public sealed record AuthResult(string AccessToken, DateTime ExpiresAtUtc, UserResponse User);

public sealed class UpdateProfileRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; init; } = "";

    [StringLength(20)]
    public string? PhoneNumber { get; init; }
}
