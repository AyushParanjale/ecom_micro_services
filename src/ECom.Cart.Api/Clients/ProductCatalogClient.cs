namespace ECom.Cart.Api.Clients;

using System.Net.Http.Json;
using ECom.Shared.Http;
using ECom.Shared.Messaging;

public interface IProductCatalogClient
{
    /// <summary>Returns the product regardless of stock level, or null when it does not exist.</summary>
    Task<ProductDto?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default);

    /// <summary>Returns every existing product among <paramref name="productIds"/>; missing ids are omitted.</summary>
    Task<IReadOnlyList<ProductDto>> GetProductsAsync(IEnumerable<Guid> productIds, CancellationToken cancellationToken = default);
}

public sealed class ProductCatalogClient : IProductCatalogClient
{
    private const string ServiceName = "Product service";
    private readonly HttpClient _http;

    public ProductCatalogClient(HttpClient http) => _http = http;

    public async Task<ProductDto?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var products = await GetProductsAsync([productId], cancellationToken);
        return products.FirstOrDefault(p => p.Id == productId);
    }

    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(IEnumerable<Guid> productIds, CancellationToken cancellationToken = default)
    {
        var ids = productIds.Distinct().ToList();
        if (ids.Count == 0)
            return Array.Empty<ProductDto>();

        using var request = new HttpRequestMessage(HttpMethod.Post, "internal/products/batch")
        {
            Content = JsonContent.Create(new { ids }, options: EComJson.Options)
        };

        using var response = await _http.SendDownstreamAsync(request, ServiceName, cancellationToken);
        await response.EnsureSuccessOrThrowAsync(ServiceName, cancellationToken);

        return await response.Content.ReadFromJsonAsync<List<ProductDto>>(EComJson.Options, cancellationToken)
            ?? new List<ProductDto>();
    }
}
