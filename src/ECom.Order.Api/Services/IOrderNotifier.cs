namespace ECom.Order.Api.Services;

using ECom.Order.Api.DTOs;

public interface IOrderNotifier
{
    Task NotifyOrderStatusChangedAsync(OrderStatusChangedNotification notification, CancellationToken cancellationToken = default);
}
