namespace ECom.Cart.UnitTests;

using ECom.Cart.Api.Messaging;
using ECom.Cart.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

[TestClass]
public sealed class OrderCreatedHandlerTests
{
    [TestMethod]
    public async Task HandleAsync_PassesUserIdAndProductIdsToCartService()
    {
        var userId = Guid.NewGuid();
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();
        var message = new OrderCreatedEvent(
            Guid.NewGuid(),
            userId,
            new[] { new OrderCreatedItem(productA, 2), new OrderCreatedItem(productB, 1) },
            DateTimeOffset.UtcNow);

        IEnumerable<Guid>? capturedIds = null;
        var cartService = new Mock<ICartService>();
        cartService
            .Setup(s => s.RemoveCheckedOutItemsAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, IEnumerable<Guid>, CancellationToken>((_, ids, _) => capturedIds = ids.ToList())
            .ReturnsAsync(2);

        var handler = new OrderCreatedHandler(cartService.Object, NullLogger<OrderCreatedHandler>.Instance);
        using var cts = new CancellationTokenSource();

        await handler.HandleAsync(message, "msg-1", cts.Token);

        cartService.Verify(
            s => s.RemoveCheckedOutItemsAsync(userId, It.IsAny<IEnumerable<Guid>>(), cts.Token),
            Times.Once);
        Assert.IsNotNull(capturedIds);
        CollectionAssert.AreEqual(new[] { productA, productB }, capturedIds.ToList());
    }
}
