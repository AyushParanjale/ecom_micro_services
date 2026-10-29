namespace ECom.Auth.Api.DTOs;

using System.ComponentModel.DataAnnotations;

public sealed class LoginRequest
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    [MaxLength(256, ErrorMessage = "Email must not exceed 256 characters.")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [MaxLength(128, ErrorMessage = "Password must not exceed 128 characters.")]
    public string Password { get; init; } = string.Empty;
}
