namespace ECom.Auth.Api.DTOs;

using System.ComponentModel.DataAnnotations;

public sealed class RegisterRequest
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    [MaxLength(256, ErrorMessage = "Email must not exceed 256 characters.")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    [MaxLength(128, ErrorMessage = "Password must not exceed 128 characters.")]
    public string Password { get; init; } = string.Empty;

    [Required(ErrorMessage = "Name is required.")]
    [MinLength(1, ErrorMessage = "Name is required.")]
    [MaxLength(200, ErrorMessage = "Name must not exceed 200 characters.")]
    public string Name { get; init; } = string.Empty;
}
