namespace ECom.Order.Api.DTOs;

public sealed record PaymentGatewayRequest(
    Guid PaymentId,
    Guid OrderId,
    decimal Amount,
    string IdempotencyKey);
