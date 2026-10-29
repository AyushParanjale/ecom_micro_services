namespace ECom.Cart.Api.Controllers;

using ECom.Cart.Api.DTOs;
using ECom.Cart.Api.Services;
using ECom.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/cart")]
[Authorize]
public sealed class CartController : ControllerBase
{
    private readonly ICartService _cartService;

    public CartController(ICartService cartService) => _cartService = cartService;

    [HttpGet]
    [ProducesResponseType<CartResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCart(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var cart = await _cartService.GetCartAsync(userId, cancellationToken);
        return Ok(cart);
    }

    [HttpPost("items")]
    [ProducesResponseType<CartResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddItem(
        [FromBody] AddCartItemRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var cart = await _cartService.AddItemAsync(userId, request, cancellationToken);
        return Ok(cart);
    }

    [HttpPut("items/{id:guid}")]
    [ProducesResponseType<CartResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateItem(
        Guid id,
        [FromBody] UpdateCartItemRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var cart = await _cartService.UpdateItemAsync(userId, id, request, cancellationToken);
        return Ok(cart);
    }

    [HttpDelete("items/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveItem(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await _cartService.RemoveItemAsync(userId, id, cancellationToken);
        return NoContent();
    }
}
