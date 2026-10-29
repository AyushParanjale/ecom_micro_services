namespace ECom.Order.Api.Controllers;

using ECom.Order.Api.DTOs;
using ECom.Order.Api.Services;
using ECom.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/checkout")]
[Authorize]
public sealed class CheckoutController : ControllerBase
{
    private readonly IOrderService _orderService;

    public CheckoutController(IOrderService orderService) => _orderService = orderService;

    /// <summary>Canonical checkout route. Creates a PendingPayment order from the current cart.</summary>
    [HttpPost]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Checkout(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var order = await _orderService.CreateOrderAsync(userId, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, order);
    }
}
