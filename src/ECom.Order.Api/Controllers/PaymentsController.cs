namespace ECom.Order.Api.Controllers;

using ECom.Order.Api.DTOs;
using ECom.Order.Api.Services;
using ECom.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/payments")]
[Authorize]
public sealed class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService) => _paymentService = paymentService;

    [HttpPost]
    [ProducesResponseType<PaymentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<PaymentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreatePayment(
        [FromBody] CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var (payment, isNew) = await _paymentService.CreatePaymentAsync(userId, request, cancellationToken);

        if (isNew)
            return StatusCode(StatusCodes.Status201Created, payment);

        return Ok(payment);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PaymentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var payment = await _paymentService.GetPaymentByIdAsync(id, userId, cancellationToken);
        return Ok(payment);
    }
}
