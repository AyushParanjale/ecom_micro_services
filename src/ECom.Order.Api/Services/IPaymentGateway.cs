namespace ECom.Order.Api.Services;

using ECom.Order.Api.DTOs;

public interface IPaymentGateway
{
    Task<PaymentGatewayResult> ProcessAsync(
        PaymentGatewayRequest request,
        CancellationToken cancellationToken = default);
}
