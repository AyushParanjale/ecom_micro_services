namespace ECom.Order.Api.Controllers;

using ECom.Order.Api.DTOs;
using ECom.Order.Api.Services;
using ECom.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/orders")]
[Authorize]
public sealed class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly IPaymentService _paymentService;

    public OrdersController(IOrderService orderService, IPaymentService paymentService)
    {
        _orderService = orderService;
        _paymentService = paymentService;
    }

    /// <summary>
    /// Deprecated: Use POST /api/checkout instead.
    /// Delegates to the same checkout logic; returns a PendingPayment order.
    /// </summary>
    [HttpPost]
    [Obsolete]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PlaceOrder(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var order = await _orderService.CreateOrderAsync(userId, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, order);
    }

    [HttpGet("my-orders")]
    [ProducesResponseType<IReadOnlyList<OrderResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyOrders(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var orders = await _orderService.GetMyOrdersAsync(userId, cancellationToken);
        return Ok(orders);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var order = await _orderService.GetOrderByIdAsync(id, userId, cancellationToken);
        return Ok(order);
    }

    [HttpGet("{orderId:guid}/payment")]
    [ProducesResponseType<PaymentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderPayment(Guid orderId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var payment = await _paymentService.GetPaymentByOrderIdAsync(orderId, userId, cancellationToken);
        return Ok(payment);
    }
}
