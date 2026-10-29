namespace ECom.Cart.Api.Services;

using ECom.Cart.Api.Clients;
using ECom.Cart.Api.DTOs;
using ECom.Cart.Api.Entities;
using ECom.Cart.Api.Repositories;
using ECom.Shared.Exceptions;

public sealed class CartService : ICartService
{
    private readonly ICartRepository _cartRepo;
    private readonly IProductCatalogClient _productClient;

    public CartService(ICartRepository cartRepo, IProductCatalogClient productClient)
    {
        _cartRepo = cartRepo;
        _productClient = productClient;
    }

    public async Task<CartResponse> GetCartAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cart = await _cartRepo.GetByUserIdWithItemsAsync(userId, cancellationToken);

        if (cart is null)
            return EmptyCartResponse(userId);

        return await BuildCartResponseAsync(cart, cancellationToken);
    }

    public async Task<CartResponse> AddItemAsync(Guid userId, AddCartItemRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ProductId == Guid.Empty)
            throw new DomainException("ProductId is required.");

        var product = await _productClient.GetProductAsync(request.ProductId, cancellationToken);
        if (product is null)
            throw new NotFoundException("Product not found.");

        if (product.StockQuantity <= 0)
            throw new DomainException("Product is out of stock.");

        if (request.Quantity > product.StockQuantity)
            throw new DomainException($"Requested quantity ({request.Quantity}) exceeds available stock ({product.StockQuantity}).");

        var cart = await _cartRepo.GetByUserIdWithItemsAsync(userId, cancellationToken);

        if (cart is null)
        {
            cart = new Cart(userId);
            cart.AddItem(product.Id, request.Quantity);
            await _cartRepo.AddAsync(cart, cancellationToken);
        }
        else
        {
            if (cart.Items.Any(i => i.ProductId == product.Id))
                throw new ConflictException("This product is already in your cart. Use PUT to update the quantity.");

            var item = cart.AddItem(product.Id, request.Quantity);
            _cartRepo.StageItem(item);
            await _cartRepo.SaveAsync(cancellationToken);
        }

        return await BuildCartResponseAsync(cart, cancellationToken);
    }

    public async Task<CartResponse> UpdateItemAsync(Guid userId, Guid itemId, UpdateCartItemRequest request, CancellationToken cancellationToken = default)
    {
        var cart = await _cartRepo.GetByUserIdWithItemsAsync(userId, cancellationToken);

        var item = cart?.Items.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
            throw new NotFoundException("Cart item not found.");

        var product = await _productClient.GetProductAsync(item.ProductId, cancellationToken);
        if (product is null)
            throw new NotFoundException("Product not found.");

        if (request.Quantity > product.StockQuantity)
            throw new DomainException($"Requested quantity ({request.Quantity}) exceeds available stock ({product.StockQuantity}).");

        cart!.UpdateItemQuantity(itemId, request.Quantity);
        await _cartRepo.SaveAsync(cancellationToken);

        return await BuildCartResponseAsync(cart, cancellationToken);
    }

    public async Task RemoveItemAsync(Guid userId, Guid itemId, CancellationToken cancellationToken = default)
    {
        var cart = await _cartRepo.GetByUserIdWithItemsAsync(userId, cancellationToken);

        var item = cart?.Items.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
            throw new NotFoundException("Cart item not found.");

        cart!.RemoveItem(itemId);
        await _cartRepo.SaveAsync(cancellationToken);
    }

    public async Task<CartSnapshotResponse> GetCartSnapshotAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cart = await _cartRepo.GetByUserIdWithItemsAsync(userId, cancellationToken);

        if (cart is null)
            return new CartSnapshotResponse(Guid.Empty, userId, Array.Empty<CartSnapshotItem>());

        return new CartSnapshotResponse(
            cart.Id,
            cart.UserId,
            cart.Items.Select(i => new CartSnapshotItem(i.Id, i.ProductId, i.Quantity)).ToList().AsReadOnly());
    }

    public async Task<int> RemoveCheckedOutItemsAsync(Guid userId, IEnumerable<Guid> productIds, CancellationToken cancellationToken = default)
    {
        var cart = await _cartRepo.GetByUserIdWithItemsAsync(userId, cancellationToken);
        if (cart is null)
            return 0;

        var checkedOut = productIds.ToHashSet();
        var toRemove = cart.Items.Where(i => checkedOut.Contains(i.ProductId)).Select(i => i.Id).ToList();
        if (toRemove.Count == 0)
            return 0;

        foreach (var itemId in toRemove)
            cart.RemoveItem(itemId);

        await _cartRepo.SaveAsync(cancellationToken);
        return toRemove.Count;
    }

    private async Task<CartResponse> BuildCartResponseAsync(Cart cart, CancellationToken cancellationToken)
    {
        if (!cart.Items.Any())
        {
            return new CartResponse(
                cart.Id,
                cart.UserId,
                Array.Empty<CartItemResponse>(),
                0,
                0m,
                cart.CreatedAt,
                cart.UpdatedAt);
        }

        var productIds = cart.Items.Select(i => i.ProductId).Distinct();
        var products = await _productClient.GetProductsAsync(productIds, cancellationToken);
        var productMap = products.ToDictionary(p => p.Id);

        var items = cart.Items
            .Select(item =>
            {
                if (!productMap.TryGetValue(item.ProductId, out var product))
                    return null;

                return new CartItemResponse(
                    item.Id,
                    item.ProductId,
                    product.Name,
                    product.ImageUrl,
                    product.Category,
                    item.Quantity,
                    product.StockQuantity,
                    product.Price,
                    product.Price * item.Quantity);
            })
            .Where(i => i is not null)
            .Cast<CartItemResponse>()
            .ToList()
            .AsReadOnly();

        return new CartResponse(
            cart.Id,
            cart.UserId,
            items,
            items.Sum(i => i.Quantity),
            items.Sum(i => i.LineTotal),
            cart.CreatedAt,
            cart.UpdatedAt);
    }

    private static CartResponse EmptyCartResponse(Guid userId) =>
        new(Guid.Empty, userId, Array.Empty<CartItemResponse>(), 0, 0m, DateTimeOffset.MinValue, DateTimeOffset.MinValue);
}
