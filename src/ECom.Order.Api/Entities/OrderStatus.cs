namespace ECom.Order.Api.Entities;

public enum OrderStatus
{
    PendingPayment = 0,
    Confirmed = 1,
    PaymentFailed = 2,
    Cancelled = 3
}
