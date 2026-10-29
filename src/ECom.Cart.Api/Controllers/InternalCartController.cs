namespace ECom.Cart.Api.Controllers;

using ECom.Cart.Api.DTOs;
using ECom.Cart.Api.Services;
using ECom.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// East-west endpoint used by the Order service at checkout. Not routed by the gateway.
/// The caller forwards the end user's JWT, so the cart returned is always the caller's own.
/// </summary>
[ApiController]
[Route("internal/cart")]
[Authorize]
public sealed class InternalCartController : ControllerBase
{
    private readonly ICartService _cartService;

    public InternalCartController(ICartService cartService) => _cartService = cartService;

    [HttpGet]
    [ProducesResponseType<CartSnapshotResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSnapshot(CancellationToken cancellationToken)
    {
        var snapshot = await _cartService.GetCartSnapshotAsync(User.GetUserId(), cancellationToken);
        return Ok(snapshot);
    }
}
