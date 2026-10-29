namespace ECom.Order.Api.Clients;

using System.Net.Http.Json;
using ECom.Shared.Http;
using ECom.Shared.Messaging;

// Local copies of the Product service's contracts.
public sealed record ProductDto(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    string Category,
    int StockQuantity,
    string? ImageUrl);

public sealed record StockLineDto(Guid ProductId, int Quantity);

public interface IProductClient
{
    /// <summary>Returns every existing product among <paramref name="productIds"/> regardless of stock; missing ids are omitted.</summary>
    Task<IReadOnlyList<ProductDto>> GetProductsAsync(IEnumerable<Guid> productIds, CancellationToken cancellationToken = default);

    /// <summary>All-or-nothing decrement, idempotent per order. Throws ConflictException on insufficient stock.</summary>
    Task DecrementStockAsync(Guid orderId, IReadOnlyList<StockLineDto> items, CancellationToken cancellationToken = default);

    /// <summary>Compensation for <see cref="DecrementStockAsync"/>. Idempotent per order.</summary>
    Task RestoreStockAsync(Guid orderId, IReadOnlyList<StockLineDto> items, CancellationToken cancellationToken = default);
}

public sealed class ProductClient : IProductClient
{
    private const string ServiceName = "Product service";
    private readonly HttpClient _http;

    public ProductClient(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(IEnumerable<Guid> productIds, CancellationToken cancellationToken = default)
    {
        var ids = productIds.Distinct().ToList();
        if (ids.Count == 0)
            return Array.Empty<ProductDto>();

        using var response = await PostAsync("internal/products/batch", new { ids }, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<ProductDto>>(EComJson.Options, cancellationToken)
            ?? new List<ProductDto>();
    }

    public async Task DecrementStockAsync(Guid orderId, IReadOnlyList<StockLineDto> items, CancellationToken cancellationToken = default)
    {
        using var _ = await PostAsync("internal/stock/decrement", new { orderId, items }, cancellationToken);
    }

    public async Task RestoreStockAsync(Guid orderId, IReadOnlyList<StockLineDto> items, CancellationToken cancellationToken = default)
    {
        using var _ = await PostAsync("internal/stock/restore", new { orderId, items }, cancellationToken);
    }

    private async Task<HttpResponseMessage> PostAsync(string path, object body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body, options: EComJson.Options)
        };

        var response = await _http.SendDownstreamAsync(request, ServiceName, cancellationToken);
        try
        {
            await response.EnsureSuccessOrThrowAsync(ServiceName, cancellationToken);
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }
}
