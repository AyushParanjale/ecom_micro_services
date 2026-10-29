namespace ECom.Order.Api.DTOs;

using System.ComponentModel.DataAnnotations;

public sealed class CreatePaymentRequest
{
    [Required]
    public Guid OrderId { get; set; }

    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string IdempotencyKey { get; set; } = string.Empty;
}
