namespace ECom.Cart.UnitTests;

using System.Net;
using System.Text;
using System.Text.Json;
using ECom.Cart.Api.Clients;
using ECom.Shared.Exceptions;

[TestClass]
public sealed class ProductCatalogClientTests
{
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        public int CallCount { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequest = request;
            if (request.Content is not null)
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            return _respond(request);
        }
    }

    private static (ProductCatalogClient client, FakeHttpMessageHandler handler) MakeClient(
        Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new FakeHttpMessageHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://product.test/") };
        return (new ProductCatalogClient(http), handler);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json, string mediaType = "application/json") =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, mediaType) };

    [TestMethod]
    public async Task GetProductsAsync_Success_DeserializesCamelCaseProducts()
    {
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var body = $$"""
            [
              {"id":"{{idA}}","name":"Widget","description":"A widget","price":9.99,"category":"Tools","stockQuantity":5,"imageUrl":"http://img/a.png"},
              {"id":"{{idB}}","name":"Gadget","description":"A gadget","price":19.5,"category":"Toys","stockQuantity":0,"imageUrl":null}
            ]
            """;
        var (client, handler) = MakeClient(_ => Json(HttpStatusCode.OK, body));

        var result = await client.GetProductsAsync(new[] { idA, idB, idA });

        Assert.HasCount(2, result);
        Assert.AreEqual(new ProductDto(idA, "Widget", "A widget", 9.99m, "Tools", 5, "http://img/a.png"), result[0]);
        Assert.AreEqual(new ProductDto(idB, "Gadget", "A gadget", 19.5m, "Toys", 0, null), result[1]);

        Assert.AreEqual(1, handler.CallCount);
        Assert.AreEqual(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.AreEqual(new Uri("http://product.test/internal/products/batch"), handler.LastRequest.RequestUri);

        using var sent = JsonDocument.Parse(handler.LastRequestBody!);
        var sentIds = sent.RootElement.GetProperty("ids").EnumerateArray().Select(e => e.GetGuid()).ToList();
        CollectionAssert.AreEqual(new[] { idA, idB }, sentIds);
    }

    [TestMethod]
    public async Task GetProductAsync_Success_ReturnsMatchingProduct()
    {
        var id = Guid.NewGuid();
        var body = $$"""[{"id":"{{id}}","name":"Widget","description":"D","price":1.25,"category":"C","stockQuantity":3,"imageUrl":null}]""";
        var (client, _) = MakeClient(_ => Json(HttpStatusCode.OK, body));

        var product = await client.GetProductAsync(id);

        Assert.IsNotNull(product);
        Assert.AreEqual(id, product.Id);
        Assert.AreEqual(1.25m, product.Price);
        Assert.AreEqual(3, product.StockQuantity);
    }

    [TestMethod]
    public async Task GetProductAsync_EmptyBatch_ReturnsNull()
    {
        var (client, _) = MakeClient(_ => Json(HttpStatusCode.OK, "[]"));

        var product = await client.GetProductAsync(Guid.NewGuid());

        Assert.IsNull(product);
    }

    [TestMethod]
    public async Task GetProductsAsync_ConflictProblemDetails_ThrowsConflictException()
    {
        const string problem = """{"type":"about:blank","title":"Stock conflict","status":409,"detail":"Not enough stock."}""";
        var (client, _) = MakeClient(_ => Json(HttpStatusCode.Conflict, problem, "application/problem+json"));

        var ex = await Assert.ThrowsExactlyAsync<ConflictException>(() =>
            client.GetProductsAsync(new[] { Guid.NewGuid() }));

        Assert.AreEqual("Not enough stock.", ex.Message);
        Assert.AreEqual("Stock conflict", ex.ConflictTitle);
    }

    [TestMethod]
    public async Task GetProductsAsync_NetworkFailure_ThrowsDownstreamServiceException()
    {
        var (client, _) = MakeClient(_ => throw new HttpRequestException("Connection refused"));

        var ex = await Assert.ThrowsExactlyAsync<DownstreamServiceException>(() =>
            client.GetProductsAsync(new[] { Guid.NewGuid() }));

        Assert.AreEqual("Product service", ex.ServiceName);
        Assert.IsInstanceOfType<HttpRequestException>(ex.InnerException);
    }

    [TestMethod]
    public async Task GetProductsAsync_EmptyIdList_MakesNoHttpCall()
    {
        var (client, handler) = MakeClient(_ => throw new AssertFailedException("No HTTP call expected."));

        var result = await client.GetProductsAsync(Array.Empty<Guid>());

        Assert.IsEmpty(result);
        Assert.AreEqual(0, handler.CallCount);
    }
}
