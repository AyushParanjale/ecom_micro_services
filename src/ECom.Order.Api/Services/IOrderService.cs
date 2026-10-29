namespace ECom.Order.Api.Services;

using ECom.Order.Api.DTOs;

public interface IOrderService
{
    Task<OrderResponse> CreateOrderAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrderResponse>> GetMyOrdersAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<OrderResponse> GetOrderByIdAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrderResponse>> GetAllOrdersAsync(CancellationToken cancellationToken = default);
}
