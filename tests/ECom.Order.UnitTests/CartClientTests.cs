namespace ECom.Order.UnitTests;

using System.Net;
using System.Text.Json;
using ECom.Order.Api.Clients;
using ECom.Shared.Exceptions;

[TestClass]
public sealed class CartClientTests
{
    [TestMethod]
    public async Task GetCart_Success_DeserializesCamelCaseSnapshot()
    {
        var cartId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var json = $$"""
            {"cartId":"{{cartId}}","userId":"{{userId}}","items":[{"itemId":"{{itemId}}","productId":"{{productId}}","quantity":3}]}
            """;
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, json);
        var client = new CartClient(FakeHttpMessageHandler.CreateClient(handler));

        var cart = await client.GetCartAsync();

        Assert.AreEqual(cartId, cart.CartId);
        Assert.AreEqual(userId, cart.UserId);
        Assert.HasCount(1, cart.Items);
        Assert.AreEqual(new CartSnapshotItemDto(itemId, productId, 3), cart.Items[0]);

        Assert.HasCount(1, handler.Requests);
        Assert.AreEqual(HttpMethod.Get, handler.Requests[0].Method);
        Assert.AreEqual("/internal/cart", handler.Requests[0].Uri!.AbsolutePath);
    }

    [TestMethod]
    public async Task GetCart_EmptyItems_ReturnsEmptySnapshot()
    {
        var json = JsonSerializer.Serialize(new { cartId = Guid.Empty, userId = Guid.Empty, items = Array.Empty<object>() });
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, json);
        var client = new CartClient(FakeHttpMessageHandler.CreateClient(handler));

        var cart = await client.GetCartAsync();

        Assert.IsEmpty(cart.Items);
    }

    [TestMethod]
    public async Task GetCart_NullBody_ThrowsDownstreamServiceException()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, "null");
        var client = new CartClient(FakeHttpMessageHandler.CreateClient(handler));

        var ex = await Assert.ThrowsExactlyAsync<DownstreamServiceException>(() => client.GetCartAsync());

        Assert.AreEqual("Cart service", ex.ServiceName);
    }

    [TestMethod]
    public async Task GetCart_Conflict_ThrowsConflictExceptionWithTitle()
    {
        var problem = """{"type":"about:blank","title":"Cart is locked","status":409,"detail":"The cart is being modified."}""";
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.Conflict, problem, "application/problem+json");
        var client = new CartClient(FakeHttpMessageHandler.CreateClient(handler));

        var ex = await Assert.ThrowsExactlyAsync<ConflictException>(() => client.GetCartAsync());

        Assert.AreEqual("Cart is locked", ex.ConflictTitle);
        Assert.AreEqual("The cart is being modified.", ex.Message);
    }

    [TestMethod]
    public async Task GetCart_NotFound_ThrowsNotFoundException()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.NotFound);
        var client = new CartClient(FakeHttpMessageHandler.CreateClient(handler));

        var ex = await Assert.ThrowsExactlyAsync<NotFoundException>(() => client.GetCartAsync());

        Assert.AreEqual("Cart service returned 404.", ex.Message);
    }

    [TestMethod]
    public async Task GetCart_ServerError_ThrowsDownstreamServiceException()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.InternalServerError);
        var client = new CartClient(FakeHttpMessageHandler.CreateClient(handler));

        var ex = await Assert.ThrowsExactlyAsync<DownstreamServiceException>(() => client.GetCartAsync());

        Assert.AreEqual("Cart service", ex.ServiceName);
    }

    [TestMethod]
    public async Task GetCart_HttpRequestException_ThrowsDownstreamServiceException()
    {
        var inner = new HttpRequestException("connection refused");
        var handler = FakeHttpMessageHandler.Throwing(inner);
        var client = new CartClient(FakeHttpMessageHandler.CreateClient(handler));

        var ex = await Assert.ThrowsExactlyAsync<DownstreamServiceException>(() => client.GetCartAsync());

        Assert.AreEqual("Cart service", ex.ServiceName);
        Assert.AreSame(inner, ex.InnerException);
    }
}
