namespace ECom.Order.Api.Hubs;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

[Authorize]
public sealed class OrderHub : Hub<IOrderClient>
{
}
