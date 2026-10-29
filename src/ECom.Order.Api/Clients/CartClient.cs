namespace ECom.Order.Api.Clients;

using System.Net.Http.Json;
using ECom.Shared.Http;
using ECom.Shared.Messaging;

// Local copy of the Cart service's internal snapshot contract.
public sealed record CartSnapshotDto(Guid CartId, Guid UserId, IReadOnlyList<CartSnapshotItemDto> Items);

public sealed record CartSnapshotItemDto(Guid ItemId, Guid ProductId, int Quantity);

public interface ICartClient
{
    /// <summary>Returns the calling user's cart lines (identified by the forwarded JWT).</summary>
    Task<CartSnapshotDto> GetCartAsync(CancellationToken cancellationToken = default);
}

public sealed class CartClient : ICartClient
{
    private const string ServiceName = "Cart service";
    private readonly HttpClient _http;

    public CartClient(HttpClient http) => _http = http;

    public async Task<CartSnapshotDto> GetCartAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "internal/cart");
        using var response = await _http.SendDownstreamAsync(request, ServiceName, cancellationToken);
        await response.EnsureSuccessOrThrowAsync(ServiceName, cancellationToken);

        return await response.Content.ReadFromJsonAsync<CartSnapshotDto>(EComJson.Options, cancellationToken)
            ?? throw new ECom.Shared.Exceptions.DownstreamServiceException(ServiceName, "Cart service returned an empty body.");
    }
}
