namespace ECom.Order.Api.Services;

using ECom.Order.Api.DTOs;

public interface IPaymentService
{
    Task<(PaymentResponse payment, bool isNew)> CreatePaymentAsync(
        Guid userId,
        CreatePaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentResponse> GetPaymentByIdAsync(
        Guid paymentId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<PaymentResponse> GetPaymentByOrderIdAsync(
        Guid orderId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
