namespace ECom.Order.Api.Hubs;

using ECom.Order.Api.DTOs;

public interface IOrderClient
{
    Task OrderStatusChanged(OrderStatusChangedNotification notification);
}
