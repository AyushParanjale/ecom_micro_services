namespace ECom.Order.Api.Services;

using System.Text.Json;
using ECom.Order.Api.Clients;
using ECom.Order.Api.DTOs;
using ECom.Order.Api.Entities;
using ECom.Order.Api.Messaging;
using ECom.Order.Api.Repositories;
using ECom.Shared.Exceptions;
using ECom.Shared.Messaging;
using ECom.Shared.Outbox;

public sealed class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepo;
    private readonly IOutboxRepository _outboxRepo;
    private readonly ICartClient _cartClient;
    private readonly IProductClient _productClient;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository orderRepo,
        IOutboxRepository outboxRepo,
        ICartClient cartClient,
        IProductClient productClient,
        ILogger<OrderService> logger)
    {
        _orderRepo = orderRepo;
        _outboxRepo = outboxRepo;
        _cartClient = cartClient;
        _productClient = productClient;
        _logger = logger;
    }

    /// <summary>
    /// Checkout. In the monolith this was one DB transaction; across services it is:
    /// read cart (Cart) → validate products (Product) → decrement stock (Product, idempotent per order)
    /// → save order + outbox rows (Order DB, one transaction). If the save fails the stock decrement is
    /// compensated. The cart is cleared asynchronously by the Cart service on <c>OrderCreated</c>.
    /// </summary>
    public async Task<OrderResponse> CreateOrderAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cart = await _cartClient.GetCartAsync(cancellationToken);

        if (cart.Items.Count == 0)
            throw new ConflictException(
                "An order cannot be placed because the cart is empty.",
                "Cart is empty");

        var productIds = cart.Items.Select(i => i.ProductId).Distinct();
        var products = await _productClient.GetProductsAsync(productIds, cancellationToken);
        var productMap = products.ToDictionary(p => p.Id);

        foreach (var item in cart.Items)
        {
            if (!productMap.ContainsKey(item.ProductId))
                throw new ConflictException(
                    "A product in the cart is no longer available.",
                    "Product unavailable");
        }

        var insufficientItems = cart.Items
            .Where(item => productMap[item.ProductId].StockQuantity < item.Quantity)
            .ToList();

        if (insufficientItems.Count > 0)
            throw new ConflictException(
                "One or more products do not have enough stock to fulfill the order.",
                "Insufficient stock");

        var totalAmount = cart.Items.Sum(i => productMap[i.ProductId].Price * i.Quantity);
        var order = new Order(userId, totalAmount);

        foreach (var item in cart.Items)
        {
            var product = productMap[item.ProductId];
            order.AddItem(product.Id, product.Name, product.Price, item.Quantity);
        }

        var stockLines = cart.Items
            .Select(i => new StockLineDto(i.ProductId, i.Quantity))
            .ToList()
            .AsReadOnly();

        // Product re-validates and decrements atomically in its own DB; a race lost here surfaces
        // as the same 409 "Insufficient stock" the monolith returned.
        await _productClient.DecrementStockAsync(order.Id, stockLines, cancellationToken);

        _orderRepo.Stage(order);

        // Stage the outbox messages atomically within the same SaveChanges boundary.
        var now = DateTimeOffset.UtcNow;
        var notification = new OrderStatusChangedNotification(
            order.Id, order.UserId, order.Status.ToString(), now);
        _outboxRepo.Stage(new OutboxMessage(
            OutboxEventTypes.OrderStatusChanged, JsonSerializer.Serialize(notification), now));

        var orderCreated = new OrderCreatedEvent(
            order.Id,
            order.UserId,
            stockLines.Select(l => new OrderLineItem(l.ProductId, l.Quantity)).ToList(),
            now);
        _outboxRepo.Stage(new OutboxMessage(
            OutboxEventTypes.OrderCreated, JsonSerializer.Serialize(orderCreated, EComJson.Options), now));

        try
        {
            await _orderRepo.SaveAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saving order {OrderId} failed; compensating stock decrement.", order.Id);
            await CompensateStockAsync(order.Id, stockLines);
            throw;
        }

        return MapToResponse(order);
    }

    public async Task<IReadOnlyList<OrderResponse>> GetMyOrdersAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var orders = await _orderRepo.GetByUserIdWithItemsAsync(userId, cancellationToken);
        return orders.Select(MapToResponse).ToList().AsReadOnly();
    }

    public async Task<OrderResponse> GetOrderByIdAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default)
    {
        var order = await _orderRepo.GetByIdWithItemsAsync(orderId, cancellationToken);
        if (order is null)
            throw new NotFoundException($"Order {orderId} not found.");
        if (order.UserId != userId)
            throw new NotFoundException($"Order {orderId} not found.");
        return MapToResponse(order);
    }

    public async Task<IReadOnlyList<OrderResponse>> GetAllOrdersAsync(CancellationToken cancellationToken = default)
    {
        var orders = await _orderRepo.GetAllWithItemsAsync(cancellationToken);
        return orders.Select(MapToResponse).ToList().AsReadOnly();
    }

    private async Task CompensateStockAsync(Guid orderId, IReadOnlyList<StockLineDto> stockLines)
    {
        // Best-effort and not tied to the (possibly cancelled) request token. Product's restore is
        // idempotent and leaves a tombstone so a late decrement for this order is refused.
        try
        {
            await _productClient.RestoreStockAsync(orderId, stockLines, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Stock compensation failed for OrderId={OrderId}; stock must be restored manually.", orderId);
        }
    }

    private static OrderResponse MapToResponse(Order order) =>
        new(
            order.Id,
            order.OrderDate,
            order.TotalAmount,
            order.Status,
            order.Items.Select(i => new OrderItemResponse(
                i.Id,
                i.ProductId,
                i.ProductName,
                i.UnitPrice,
                i.Quantity,
                i.LineTotal)).ToList().AsReadOnly());
}
