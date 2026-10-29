namespace ECom.Order.Api.DTOs;

using ECom.Order.Api.Entities;


public sealed record PaymentResponse(
    Guid Id,
    Guid OrderId,
    decimal Amount,
    PaymentStatus Status,
    string? ProviderReference,
    string? FailureReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
