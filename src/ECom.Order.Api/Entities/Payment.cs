namespace ECom.Order.Api.Entities;

using ECom.Shared.Exceptions;

public sealed class Payment
{
    private const int MaxIdempotencyKeyLength = 128;
    private const int MaxProviderReferenceLength = 200;
    private const int MaxFailureReasonLength = 500;

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid UserId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string IdempotencyKey { get; private set; }
    public string? ProviderReference { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public byte[]? RowVersion { get; private set; }

    private Payment()
    {
        IdempotencyKey = string.Empty;
    }

    public Payment(Guid orderId, Guid userId, decimal amount, string idempotencyKey)
    {
        if (orderId == Guid.Empty)
            throw new ArgumentException("OrderId must not be empty.", nameof(orderId));
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId must not be empty.", nameof(userId));
        if (amount <= 0)
            throw new DomainException("Payment amount must be greater than zero.");

        var trimmedKey = idempotencyKey?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedKey))
            throw new DomainException("IdempotencyKey is required.");
        if (trimmedKey.Length > MaxIdempotencyKeyLength)
            throw new DomainException($"IdempotencyKey must not exceed {MaxIdempotencyKeyLength} characters.");

        Id = Guid.NewGuid();
        OrderId = orderId;
        UserId = userId;
        Amount = amount;
        Status = PaymentStatus.Pending;
        IdempotencyKey = trimmedKey;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkSucceeded(string providerReference, DateTimeOffset now)
    {
        if (Status != PaymentStatus.Pending)
            throw new DomainException("Only Pending payments can be marked Succeeded.");

        var trimmedRef = providerReference?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedRef))
            throw new DomainException("ProviderReference is required when marking payment Succeeded.");
        if (trimmedRef.Length > MaxProviderReferenceLength)
            throw new DomainException($"ProviderReference must not exceed {MaxProviderReferenceLength} characters.");

        Status = PaymentStatus.Succeeded;
        ProviderReference = trimmedRef;
        UpdatedAt = now;
    }

    public void MarkFailed(string? failureReason, DateTimeOffset now)
    {
        if (Status != PaymentStatus.Pending)
            throw new DomainException("Only Pending payments can be marked Failed.");

        if (failureReason is not null && failureReason.Length > MaxFailureReasonLength)
            throw new DomainException($"FailureReason must not exceed {MaxFailureReasonLength} characters.");

        Status = PaymentStatus.Failed;
        FailureReason = failureReason;
        UpdatedAt = now;
    }
}
