namespace ECom.Cart.Api.DTOs;

using System.ComponentModel.DataAnnotations;

public sealed record UpdateCartItemRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be greater than zero.")]
    public int Quantity { get; init; }
}
