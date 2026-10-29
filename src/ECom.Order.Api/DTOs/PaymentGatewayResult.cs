namespace ECom.Order.Api.DTOs;

public sealed record PaymentGatewayResult(
    bool IsSuccessful,
    string? ProviderReference,
    string? FailureReason);
