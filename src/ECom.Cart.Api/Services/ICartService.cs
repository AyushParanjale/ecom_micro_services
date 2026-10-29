namespace ECom.Cart.Api.Services;

using ECom.Cart.Api.DTOs;

public interface ICartService
{
    Task<CartResponse> GetCartAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<CartResponse> AddItemAsync(Guid userId, AddCartItemRequest request, CancellationToken cancellationToken = default);
    Task<CartResponse> UpdateItemAsync(Guid userId, Guid itemId, UpdateCartItemRequest request, CancellationToken cancellationToken = default);
    Task RemoveItemAsync(Guid userId, Guid itemId, CancellationToken cancellationToken = default);

    /// <summary>Raw cart lines (no product enrichment) for the Order service's checkout.</summary>
    Task<CartSnapshotResponse> GetCartSnapshotAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Removes the lines that were checked out. Idempotent; returns the number of lines removed.</summary>
    Task<int> RemoveCheckedOutItemsAsync(Guid userId, IEnumerable<Guid> productIds, CancellationToken cancellationToken = default);
}
