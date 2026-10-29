namespace ECom.Order.Api.Services;

using ECom.Order.Api.DTOs;
using ECom.Order.Api.Options;
using Microsoft.Extensions.Options;

// Development-only fake gateway. No real payment is processed.
public sealed class FakePaymentGateway : IPaymentGateway
{
    private readonly FakePaymentOptions _options;

    public FakePaymentGateway(IOptions<FakePaymentOptions> options) =>
        _options = options.Value;

    public Task<PaymentGatewayResult> ProcessAsync(
        PaymentGatewayRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_options.AlwaysFail)
        {
            return Task.FromResult(new PaymentGatewayResult(
                IsSuccessful: false,
                ProviderReference: null,
                FailureReason: "The development payment gateway declined the payment."));
        }

        var providerReference = $"fake_{request.PaymentId:N}";
        return Task.FromResult(new PaymentGatewayResult(
            IsSuccessful: true,
            ProviderReference: providerReference,
            FailureReason: null));
    }
}
