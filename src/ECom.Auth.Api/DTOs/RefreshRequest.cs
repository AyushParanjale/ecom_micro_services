namespace ECom.Auth.Api.DTOs;

using System.ComponentModel.DataAnnotations;

public sealed class RefreshRequest
{
    [Required(ErrorMessage = "RefreshToken is required.")]
    [MaxLength(512, ErrorMessage = "RefreshToken must not exceed 512 characters.")]
    public string RefreshToken { get; init; } = string.Empty;
}
