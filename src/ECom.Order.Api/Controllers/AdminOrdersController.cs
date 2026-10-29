namespace ECom.Order.Api.Controllers;

using ECom.Order.Api.DTOs;
using ECom.Order.Api.Services;
using ECom.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/admin/orders")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminOrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public AdminOrdersController(IOrderService orderService) => _orderService = orderService;

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<OrderResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAllOrders(CancellationToken cancellationToken)
    {
        var orders = await _orderService.GetAllOrdersAsync(cancellationToken);
        return Ok(orders);
    }
}
