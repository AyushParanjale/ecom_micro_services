namespace ECom.Order.Api.Hubs;

using ECom.Order.Api.Services;
using ECom.Order.Api.DTOs;
using Microsoft.AspNetCore.SignalR;

public sealed class SignalROrderNotifier : IOrderNotifier
{
    private readonly IHubContext<OrderHub, IOrderClient> _hubContext;

    public SignalROrderNotifier(IHubContext<OrderHub, IOrderClient> hubContext)
        => _hubContext = hubContext;

    public Task NotifyOrderStatusChangedAsync(
        OrderStatusChangedNotification notification,
        CancellationToken cancellationToken = default)
        => _hubContext.Clients.User(notification.UserId.ToString())
            .OrderStatusChanged(notification);
}
