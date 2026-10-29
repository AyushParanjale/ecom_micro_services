namespace ECom.Order.Api.Options;

public sealed class FakePaymentOptions
{
    public const string SectionName = "FakePayment";

    public bool AlwaysFail { get; set; } = false;
}
