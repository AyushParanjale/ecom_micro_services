namespace ECom.Cart.Api.DTOs;

public sealed record CartSnapshotItem(Guid ItemId, Guid ProductId, int Quantity);

public sealed record CartSnapshotResponse(Guid CartId, Guid UserId, IReadOnlyList<CartSnapshotItem> Items);
