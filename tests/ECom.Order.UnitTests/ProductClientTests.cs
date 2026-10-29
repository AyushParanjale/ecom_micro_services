namespace ECom.Order.UnitTests;

using System.Net;
using System.Text.Json;
using ECom.Order.Api.Clients;
using ECom.Shared.Exceptions;

[TestClass]
public sealed class ProductClientTests
{
    private const string InsufficientStockProblem =
        """{"title":"Insufficient stock","status":409,"detail":"One or more products do not have enough stock to fulfill the order."}""";

    [TestMethod]
    public async Task GetProducts_Success_DeserializesProducts()
    {
        var id = Guid.NewGuid();
        var json = $$"""
            [{"id":"{{id}}","name":"Widget","description":"Desc","price":10.50,"category":"Cat","stockQuantity":7,"imageUrl":null}]
            """;
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, json);
        var client = new ProductClient(FakeHttpMessageHandler.CreateClient(handler));

        var products = await client.GetProductsAsync(new[] { id, id });

        Assert.HasCount(1, products);
        Assert.AreEqual(new ProductDto(id, "Widget", "Desc", 10.50m, "Cat", 7, null), products[0]);

        Assert.HasCount(1, handler.Requests);
        Assert.AreEqual(HttpMethod.Post, handler.Requests[0].Method);
        Assert.AreEqual("/internal/products/batch", handler.Requests[0].Uri!.AbsolutePath);
        using var body = JsonDocument.Parse(handler.Requests[0].Body!);
        var ids = body.RootElement.GetProperty("ids").EnumerateArray().Select(e => e.GetGuid()).ToList();
        CollectionAssert.AreEqual(new[] { id }, ids);
    }

    [TestMethod]
    public async Task GetProducts_NoIds_ReturnsEmptyWithoutHttpCall()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, "[]");
        var client = new ProductClient(FakeHttpMessageHandler.CreateClient(handler));

        var products = await client.GetProductsAsync(Array.Empty<Guid>());

        Assert.IsEmpty(products);
        Assert.IsEmpty(handler.Requests);
    }

    [TestMethod]
    public async Task GetProducts_NotFound_ThrowsNotFoundException()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.NotFound);
        var client = new ProductClient(FakeHttpMessageHandler.CreateClient(handler));

        await Assert.ThrowsExactlyAsync<NotFoundException>(() => client.GetProductsAsync(new[] { Guid.NewGuid() }));
    }

    [TestMethod]
    public async Task GetProducts_HttpRequestException_ThrowsDownstreamServiceException()
    {
        var handler = FakeHttpMessageHandler.Throwing(new HttpRequestException("connection refused"));
        var client = new ProductClient(FakeHttpMessageHandler.CreateClient(handler));

        var ex = await Assert.ThrowsExactlyAsync<DownstreamServiceException>(() =>
            client.GetProductsAsync(new[] { Guid.NewGuid() }));

        Assert.AreEqual("Product service", ex.ServiceName);
        Assert.IsInstanceOfType<HttpRequestException>(ex.InnerException);
    }

    [TestMethod]
    public async Task DecrementStock_Success_PostsOrderIdAndItems()
    {
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.NoContent);
        var client = new ProductClient(FakeHttpMessageHandler.CreateClient(handler));

        await client.DecrementStockAsync(orderId, new[] { new StockLineDto(productId, 4) });

        Assert.HasCount(1, handler.Requests);
        Assert.AreEqual(HttpMethod.Post, handler.Requests[0].Method);
        Assert.AreEqual("/internal/stock/decrement", handler.Requests[0].Uri!.AbsolutePath);
        using var body = JsonDocument.Parse(handler.Requests[0].Body!);
        Assert.AreEqual(orderId, body.RootElement.GetProperty("orderId").GetGuid());
        var item = body.RootElement.GetProperty("items")[0];
        Assert.AreEqual(productId, item.GetProperty("productId").GetGuid());
        Assert.AreEqual(4, item.GetProperty("quantity").GetInt32());
    }

    [TestMethod]
    public async Task DecrementStock_Conflict_ThrowsConflictExceptionWithTitle()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.Conflict, InsufficientStockProblem, "application/problem+json");
        var client = new ProductClient(FakeHttpMessageHandler.CreateClient(handler));

        var ex = await Assert.ThrowsExactlyAsync<ConflictException>(() =>
            client.DecrementStockAsync(Guid.NewGuid(), new[] { new StockLineDto(Guid.NewGuid(), 1) }));

        Assert.AreEqual("Insufficient stock", ex.ConflictTitle);
        Assert.AreEqual("One or more products do not have enough stock to fulfill the order.", ex.Message);
    }

    [TestMethod]
    public async Task DecrementStock_ConflictWithoutProblemBody_ThrowsConflictExceptionWithoutTitle()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.Conflict);
        var client = new ProductClient(FakeHttpMessageHandler.CreateClient(handler));

        var ex = await Assert.ThrowsExactlyAsync<ConflictException>(() =>
            client.DecrementStockAsync(Guid.NewGuid(), new[] { new StockLineDto(Guid.NewGuid(), 1) }));

        Assert.IsNull(ex.ConflictTitle);
        Assert.AreEqual("Product service returned 409.", ex.Message);
    }

    [TestMethod]
    public async Task DecrementStock_NotFound_ThrowsNotFoundException()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.NotFound,
            """{"title":"Not Found","status":404,"detail":"Product x not found."}""", "application/problem+json");
        var client = new ProductClient(FakeHttpMessageHandler.CreateClient(handler));

        var ex = await Assert.ThrowsExactlyAsync<NotFoundException>(() =>
            client.DecrementStockAsync(Guid.NewGuid(), new[] { new StockLineDto(Guid.NewGuid(), 1) }));

        Assert.AreEqual("Product x not found.", ex.Message);
    }

    [TestMethod]
    public async Task DecrementStock_BadRequest_ThrowsDomainException()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.BadRequest,
            """{"title":"Bad Request","status":400,"detail":"Quantity must be greater than zero."}""", "application/problem+json");
        var client = new ProductClient(FakeHttpMessageHandler.CreateClient(handler));

        var ex = await Assert.ThrowsExactlyAsync<DomainException>(() =>
            client.DecrementStockAsync(Guid.NewGuid(), new[] { new StockLineDto(Guid.NewGuid(), 0) }));

        Assert.AreEqual("Quantity must be greater than zero.", ex.Message);
    }

    [TestMethod]
    public async Task DecrementStock_HttpRequestException_ThrowsDownstreamServiceException()
    {
        var handler = FakeHttpMessageHandler.Throwing(new HttpRequestException("connection refused"));
        var client = new ProductClient(FakeHttpMessageHandler.CreateClient(handler));

        var ex = await Assert.ThrowsExactlyAsync<DownstreamServiceException>(() =>
            client.DecrementStockAsync(Guid.NewGuid(), new[] { new StockLineDto(Guid.NewGuid(), 1) }));

        Assert.AreEqual("Product service", ex.ServiceName);
    }

    [TestMethod]
    public async Task RestoreStock_Success_PostsToRestoreEndpoint()
    {
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.NoContent);
        var client = new ProductClient(FakeHttpMessageHandler.CreateClient(handler));

        await client.RestoreStockAsync(orderId, new[] { new StockLineDto(productId, 2) });

        Assert.HasCount(1, handler.Requests);
        Assert.AreEqual("/internal/stock/restore", handler.Requests[0].Uri!.AbsolutePath);
        using var body = JsonDocument.Parse(handler.Requests[0].Body!);
        Assert.AreEqual(orderId, body.RootElement.GetProperty("orderId").GetGuid());
        Assert.AreEqual(2, body.RootElement.GetProperty("items")[0].GetProperty("quantity").GetInt32());
    }

    [TestMethod]
    public async Task RestoreStock_ServerError_ThrowsDownstreamServiceException()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.ServiceUnavailable);
        var client = new ProductClient(FakeHttpMessageHandler.CreateClient(handler));

        var ex = await Assert.ThrowsExactlyAsync<DownstreamServiceException>(() =>
            client.RestoreStockAsync(Guid.NewGuid(), new[] { new StockLineDto(Guid.NewGuid(), 1) }));

        Assert.AreEqual("Product service", ex.ServiceName);
    }
}
