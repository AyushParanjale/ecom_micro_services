namespace ECom.Order.Api.Services;

using System.Text.Json;
using ECom.Order.Api.DTOs;
using ECom.Order.Api.Entities;
using ECom.Order.Api.Messaging;
using ECom.Order.Api.Repositories;
using ECom.Shared.Exceptions;
using ECom.Shared.Messaging;
using ECom.Shared.Outbox;

public sealed class PaymentService : IPaymentService
{
    private readonly IPaymentRepository _paymentRepo;
    private readonly IOrderRepository _orderRepo;
    private readonly IPaymentGateway _gateway;
    private readonly IOutboxRepository _outboxRepo;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IPaymentRepository paymentRepo,
        IOrderRepository orderRepo,
        IPaymentGateway gateway,
        IOutboxRepository outboxRepo,
        ILogger<PaymentService> logger)
    {
        _paymentRepo = paymentRepo;
        _orderRepo = orderRepo;
        _gateway = gateway;
        _outboxRepo = outboxRepo;
        _logger = logger;
    }

    public async Task<(PaymentResponse payment, bool isNew)> CreatePaymentAsync(
        Guid userId,
        CreatePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var idempotencyKey = request.IdempotencyKey.Trim();

        var existingByKey = await _paymentRepo.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);

        if (existingByKey is not null)
        {
            if (existingByKey.OrderId != request.OrderId)
                throw new ConflictException(
                    "The idempotency key is already associated with a different order.",
                    "Idempotency conflict");

            if (existingByKey.Status != PaymentStatus.Pending)
            {
                _logger.LogInformation(
                    "Idempotent replay for PaymentId={PaymentId} OrderId={OrderId}",
                    existingByKey.Id, existingByKey.OrderId);
                return (MapToResponse(existingByKey), false);
            }

            return await ProcessGatewayAsync(existingByKey, userId, cancellationToken);
        }

        var order = await _orderRepo.GetByIdWithItemsTrackedAsync(request.OrderId, cancellationToken);
        if (order is null || order.UserId != userId)
            throw new NotFoundException($"Order {request.OrderId} not found.");

        if (order.Status != OrderStatus.PendingPayment)
            throw new ConflictException(
                "The order is not in a payable state.",
                "Order cannot be paid");

        var existingByOrder = await _paymentRepo.GetByOrderIdAsync(request.OrderId, cancellationToken);
        if (existingByOrder is not null)
            throw new ConflictException(
                "A payment already exists for this order.",
                "Payment already exists");

        var payment = new Payment(order.Id, userId, order.TotalAmount, idempotencyKey);
        _paymentRepo.Stage(payment);
        await _paymentRepo.SaveAsync(cancellationToken);

        _logger.LogInformation(
            "Created Pending Payment PaymentId={PaymentId} OrderId={OrderId}",
            payment.Id, payment.OrderId);

        return await ProcessGatewayAsync(payment, userId, cancellationToken);
    }

    private async Task<(PaymentResponse payment, bool isNew)> ProcessGatewayAsync(
        Payment payment,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var gatewayRequest = new PaymentGatewayRequest(
            payment.Id,
            payment.OrderId,
            payment.Amount,
            payment.IdempotencyKey);

        PaymentGatewayResult gatewayResult;
        try
        {
            gatewayResult = await _gateway.ProcessAsync(gatewayRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gateway technical failure for PaymentId={PaymentId}", payment.Id);
            throw new GatewayException("The payment service is temporarily unavailable. Please retry.");
        }

        var trackedPayment = await _paymentRepo.GetByIdTrackedAsync(payment.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Payment {payment.Id} not found after creation.");

        var trackedOrder = await _orderRepo.GetByIdWithItemsTrackedAsync(payment.OrderId, cancellationToken)
            ?? throw new InvalidOperationException($"Order {payment.OrderId} not found during payment processing.");

        var now = DateTimeOffset.UtcNow;

        if (gatewayResult.IsSuccessful)
        {
            trackedPayment.MarkSucceeded(gatewayResult.ProviderReference!, now);
            trackedOrder.ConfirmPayment();

            StageOutboxMessage(trackedOrder, now);
            await _paymentRepo.SaveAsync(cancellationToken);

            _logger.LogInformation(
                "Payment succeeded PaymentId={PaymentId} OrderId={OrderId}",
                trackedPayment.Id, trackedOrder.Id);
        }
        else
        {
            trackedPayment.MarkFailed(gatewayResult.FailureReason, now);
            trackedOrder.MarkPaymentFailed();

            StageOutboxMessage(trackedOrder, now);

            // Stock was decremented by the Product service at checkout; ask it (asynchronously, via
            // the outbox in this same transaction) to put the stock back.
            StagePaymentFailedEvent(trackedOrder, now);
            await _paymentRepo.SaveAsync(cancellationToken);

            _logger.LogWarning(
                "Payment declined PaymentId={PaymentId} OrderId={OrderId} Reason={Reason}",
                trackedPayment.Id, trackedOrder.Id, gatewayResult.FailureReason);
        }

        return (MapToResponse(trackedPayment), true);
    }

    private void StageOutboxMessage(Order order, DateTimeOffset now)
    {
        var notification = new OrderStatusChangedNotification(
            order.Id, order.UserId, order.Status.ToString(), now);
        var outboxMsg = new OutboxMessage(
            OutboxEventTypes.OrderStatusChanged,
            JsonSerializer.Serialize(notification),
            now);
        _outboxRepo.Stage(outboxMsg);
    }

    private void StagePaymentFailedEvent(Order order, DateTimeOffset now)
    {
        var paymentFailed = new OrderPaymentFailedEvent(
            order.Id,
            order.UserId,
            order.Items.Select(i => new OrderLineItem(i.ProductId, i.Quantity)).ToList(),
            now);
        _outboxRepo.Stage(new OutboxMessage(
            OutboxEventTypes.OrderPaymentFailed,
            JsonSerializer.Serialize(paymentFailed, EComJson.Options),
            now));
    }

    public async Task<PaymentResponse> GetPaymentByIdAsync(
        Guid paymentId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var payment = await _paymentRepo.GetByIdForUserAsync(paymentId, userId, cancellationToken);
        if (payment is null)
            throw new NotFoundException($"Payment {paymentId} not found.");
        return MapToResponse(payment);
    }

    public async Task<PaymentResponse> GetPaymentByOrderIdAsync(
        Guid orderId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var payment = await _paymentRepo.GetByOrderIdForUserAsync(orderId, userId, cancellationToken);
        if (payment is null)
            throw new NotFoundException($"Payment for order {orderId} not found.");
        return MapToResponse(payment);
    }

    private static PaymentResponse MapToResponse(Payment payment) =>
        new(
            payment.Id,
            payment.OrderId,
            payment.Amount,
            payment.Status,
            payment.ProviderReference,
            payment.FailureReason,
            payment.CreatedAt,
            payment.UpdatedAt);
}
