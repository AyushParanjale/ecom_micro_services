namespace ECom.Cart.UnitTests;

using ECom.Cart.Api.Clients;
using ECom.Cart.Api.DTOs;
using ECom.Cart.Api.Entities;
using ECom.Cart.Api.Repositories;
using ECom.Cart.Api.Services;
using ECom.Shared.Exceptions;
using Moq;

[TestClass]
public sealed class CartServiceTests
{
    private static ProductDto MakeProduct(string name = "Widget", int stock = 10) =>
        new(Guid.NewGuid(), name, "Description", 9.99m, "Category", stock, null);

    private static (Mock<ICartRepository> cartRepo, Mock<IProductCatalogClient> productClient) MakeMocks()
    {
        var cartRepo = new Mock<ICartRepository>();
        cartRepo.Setup(r => r.GetByUserIdWithItemsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cart?)null);
        cartRepo.Setup(r => r.AddAsync(It.IsAny<Cart>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        cartRepo.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var productClient = new Mock<IProductCatalogClient>();
        productClient.Setup(r => r.GetProductAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductDto?)null);
        productClient.Setup(r => r.GetProductsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductDto>().AsReadOnly());

        return (cartRepo, productClient);
    }

    private static CartService MakeSvc(Mock<ICartRepository> cartRepo, Mock<IProductCatalogClient> productClient) =>
        new(cartRepo.Object, productClient.Object);

    // --- GetCart ---

    [TestMethod]
    public async Task GetCart_NullCart_ReturnsEmptyCartResponse()
    {
        var (cartRepo, productClient) = MakeMocks();
        var svc = MakeSvc(cartRepo, productClient);
        var userId = Guid.NewGuid();

        var result = await svc.GetCartAsync(userId);

        Assert.AreEqual(Guid.Empty, result.Id);
        Assert.AreEqual(userId, result.UserId);
        Assert.AreEqual(0, result.TotalItems);
        Assert.AreEqual(0m, result.TotalAmount);
        Assert.IsNotNull(result.Items);
        Assert.IsEmpty(result.Items);
    }

    [TestMethod]
    public async Task GetCart_WithItems_ReturnsTotals()
    {
        var (cartRepo, productClient) = MakeMocks();
        var userId = Guid.NewGuid();
        var product = MakeProduct(stock: 10);

        var cart = new Cart(userId);
        cart.AddItem(product.Id, 3);

        cartRepo.Setup(r => r.GetByUserIdWithItemsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        productClient.Setup(r => r.GetProductsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductDto> { product }.AsReadOnly());

        var svc = MakeSvc(cartRepo, productClient);
        var result = await svc.GetCartAsync(userId);

        Assert.AreEqual(cart.Id, result.Id);
        Assert.HasCount(1, result.Items);
        Assert.AreEqual(3, result.TotalItems);
        Assert.AreEqual(product.Price * 3, result.TotalAmount);
    }

    [TestMethod]
    public async Task GetCart_EmptyItemsList_ReturnsTotalsOfZero()
    {
        var (cartRepo, productClient) = MakeMocks();
        var userId = Guid.NewGuid();
        var cart = new Cart(userId);

        cartRepo.Setup(r => r.GetByUserIdWithItemsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);

        var svc = MakeSvc(cartRepo, productClient);
        var result = await svc.GetCartAsync(userId);

        Assert.AreEqual(cart.Id, result.Id);
        Assert.AreEqual(0, result.TotalItems);
        Assert.AreEqual(0m, result.TotalAmount);
        Assert.IsEmpty(result.Items);
    }

    // --- AddItem ---

    [TestMethod]
    public async Task AddItem_NewUser_CreatesCartAndItem()
    {
        var (cartRepo, productClient) = MakeMocks();
        var userId = Guid.NewGuid();
        var product = MakeProduct(stock: 10);

        productClient.Setup(r => r.GetProductAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        productClient.Setup(r => r.GetProductsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductDto> { product }.AsReadOnly());

        var svc = MakeSvc(cartRepo, productClient);
        var result = await svc.AddItemAsync(userId, new AddCartItemRequest { ProductId = product.Id, Quantity = 2 });

        cartRepo.Verify(r => r.AddAsync(It.IsAny<Cart>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.HasCount(1, result.Items);
        Assert.AreEqual(product.Id, result.Items[0].ProductId);
        Assert.AreEqual(2, result.Items[0].Quantity);
    }

    [TestMethod]
    public async Task AddItem_ExistingCart_AddsItemAndSaves()
    {
        var (cartRepo, productClient) = MakeMocks();
        var userId = Guid.NewGuid();
        var existingProduct = MakeProduct("Existing", 5);
        var newProduct = MakeProduct("New", 10);

        var cart = new Cart(userId);
        cart.AddItem(existingProduct.Id, 1);

        cartRepo.Setup(r => r.GetByUserIdWithItemsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        productClient.Setup(r => r.GetProductAsync(newProduct.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(newProduct);
        productClient.Setup(r => r.GetProductsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductDto> { existingProduct, newProduct }.AsReadOnly());

        var svc = MakeSvc(cartRepo, productClient);
        var result = await svc.AddItemAsync(userId, new AddCartItemRequest { ProductId = newProduct.Id, Quantity = 3 });

        cartRepo.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
        cartRepo.Verify(r => r.AddAsync(It.IsAny<Cart>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.HasCount(2, result.Items);
    }

    [TestMethod]
    public async Task AddItem_UnknownProduct_ThrowsNotFoundException()
    {
        var (cartRepo, productClient) = MakeMocks();
        var svc = MakeSvc(cartRepo, productClient);

        await Assert.ThrowsExactlyAsync<NotFoundException>(() =>
            svc.AddItemAsync(Guid.NewGuid(), new AddCartItemRequest { ProductId = Guid.NewGuid(), Quantity = 1 }));
    }

    [TestMethod]
    public async Task AddItem_OutOfStockProduct_ThrowsDomainException()
    {
        var (cartRepo, productClient) = MakeMocks();
        var product = MakeProduct(stock: 0);

        productClient.Setup(r => r.GetProductAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var svc = MakeSvc(cartRepo, productClient);
        await Assert.ThrowsExactlyAsync<DomainException>(() =>
            svc.AddItemAsync(Guid.NewGuid(), new AddCartItemRequest { ProductId = product.Id, Quantity = 1 }));
    }

    [TestMethod]
    public async Task AddItem_QuantityExceedsStock_ThrowsDomainException()
    {
        var (cartRepo, productClient) = MakeMocks();
        var product = MakeProduct(stock: 3);

        productClient.Setup(r => r.GetProductAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var svc = MakeSvc(cartRepo, productClient);
        await Assert.ThrowsExactlyAsync<DomainException>(() =>
            svc.AddItemAsync(Guid.NewGuid(), new AddCartItemRequest { ProductId = product.Id, Quantity = 5 }));
    }

    [TestMethod]
    public async Task AddItem_DuplicateProduct_ThrowsConflictException()
    {
        var (cartRepo, productClient) = MakeMocks();
        var userId = Guid.NewGuid();
        var product = MakeProduct(stock: 10);

        var cart = new Cart(userId);
        cart.AddItem(product.Id, 1);

        cartRepo.Setup(r => r.GetByUserIdWithItemsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        productClient.Setup(r => r.GetProductAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var svc = MakeSvc(cartRepo, productClient);
        await Assert.ThrowsExactlyAsync<ConflictException>(() =>
            svc.AddItemAsync(userId, new AddCartItemRequest { ProductId = product.Id, Quantity = 1 }));
    }

    [TestMethod]
    public async Task AddItem_EmptyProductId_ThrowsDomainException()
    {
        var (cartRepo, productClient) = MakeMocks();
        var svc = MakeSvc(cartRepo, productClient);

        await Assert.ThrowsExactlyAsync<DomainException>(() =>
            svc.AddItemAsync(Guid.NewGuid(), new AddCartItemRequest { ProductId = Guid.Empty, Quantity = 1 }));
    }

    // --- UpdateItem ---

    [TestMethod]
    public async Task UpdateItem_ValidQuantity_UpdatesQuantity()
    {
        var (cartRepo, productClient) = MakeMocks();
        var userId = Guid.NewGuid();
        var product = MakeProduct(stock: 20);

        var cart = new Cart(userId);
        var item = cart.AddItem(product.Id, 2);

        cartRepo.Setup(r => r.GetByUserIdWithItemsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        productClient.Setup(r => r.GetProductAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        productClient.Setup(r => r.GetProductsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductDto> { product }.AsReadOnly());

        var svc = MakeSvc(cartRepo, productClient);
        var result = await svc.UpdateItemAsync(userId, item.Id, new UpdateCartItemRequest { Quantity = 5 });

        cartRepo.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.HasCount(1, result.Items);
        Assert.AreEqual(5, result.Items[0].Quantity);
    }

    [TestMethod]
    public async Task UpdateItem_CartNotFound_ThrowsNotFoundException()
    {
        var (cartRepo, productClient) = MakeMocks();
        var svc = MakeSvc(cartRepo, productClient);

        await Assert.ThrowsExactlyAsync<NotFoundException>(() =>
            svc.UpdateItemAsync(Guid.NewGuid(), Guid.NewGuid(), new UpdateCartItemRequest { Quantity = 1 }));
    }

    [TestMethod]
    public async Task UpdateItem_ItemNotInCart_ThrowsNotFoundException()
    {
        var (cartRepo, productClient) = MakeMocks();
        var userId = Guid.NewGuid();
        var cart = new Cart(userId);

        cartRepo.Setup(r => r.GetByUserIdWithItemsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);

        var svc = MakeSvc(cartRepo, productClient);
        await Assert.ThrowsExactlyAsync<NotFoundException>(() =>
            svc.UpdateItemAsync(userId, Guid.NewGuid(), new UpdateCartItemRequest { Quantity = 1 }));
    }

    [TestMethod]
    public async Task UpdateItem_QuantityExceedsStock_ThrowsDomainException()
    {
        var (cartRepo, productClient) = MakeMocks();
        var userId = Guid.NewGuid();
        var product = MakeProduct(stock: 5);

        var cart = new Cart(userId);
        var item = cart.AddItem(product.Id, 2);

        cartRepo.Setup(r => r.GetByUserIdWithItemsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        productClient.Setup(r => r.GetProductAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var svc = MakeSvc(cartRepo, productClient);
        await Assert.ThrowsExactlyAsync<DomainException>(() =>
            svc.UpdateItemAsync(userId, item.Id, new UpdateCartItemRequest { Quantity = 10 }));
    }

    [TestMethod]
    public async Task UpdateItem_ZeroQuantity_ThrowsDomainException()
    {
        var (cartRepo, productClient) = MakeMocks();
        var userId = Guid.NewGuid();
        var product = MakeProduct(stock: 10);

        var cart = new Cart(userId);
        var item = cart.AddItem(product.Id, 2);

        cartRepo.Setup(r => r.GetByUserIdWithItemsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        productClient.Setup(r => r.GetProductAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var svc = MakeSvc(cartRepo, productClient);
        await Assert.ThrowsExactlyAsync<DomainException>(() =>
            svc.UpdateItemAsync(userId, item.Id, new UpdateCartItemRequest { Quantity = 0 }));
    }

    // --- RemoveItem ---

    [TestMethod]
    public async Task RemoveItem_ValidItem_CallsSaveAsync()
    {
        var (cartRepo, productClient) = MakeMocks();
        var userId = Guid.NewGuid();
        var product = MakeProduct(stock: 10);

        var cart = new Cart(userId);
        var item = cart.AddItem(product.Id, 1);

        cartRepo.Setup(r => r.GetByUserIdWithItemsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);

        var svc = MakeSvc(cartRepo, productClient);
        await svc.RemoveItemAsync(userId, item.Id);

        cartRepo.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task RemoveItem_CartNotFound_ThrowsNotFoundException()
    {
        var (cartRepo, productClient) = MakeMocks();
        var svc = MakeSvc(cartRepo, productClient);

        await Assert.ThrowsExactlyAsync<NotFoundException>(() =>
            svc.RemoveItemAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [TestMethod]
    public async Task RemoveItem_ItemNotInCart_ThrowsNotFoundException()
    {
        var (cartRepo, productClient) = MakeMocks();
        var userId = Guid.NewGuid();
        var cart = new Cart(userId);

        cartRepo.Setup(r => r.GetByUserIdWithItemsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);

        var svc = MakeSvc(cartRepo, productClient);
        await Assert.ThrowsExactlyAsync<NotFoundException>(() =>
            svc.RemoveItemAsync(userId, Guid.NewGuid()));
    }

    // --- GetCartSnapshot ---

    [TestMethod]
    public async Task GetCartSnapshot_NullCart_ReturnsEmptySnapshot()
    {
        var (cartRepo, productClient) = MakeMocks();
        var svc = MakeSvc(cartRepo, productClient);
        var userId = Guid.NewGuid();

        var result = await svc.GetCartSnapshotAsync(userId);

        Assert.AreEqual(Guid.Empty, result.CartId);
        Assert.AreEqual(userId, result.UserId);
        Assert.IsNotNull(result.Items);
        Assert.IsEmpty(result.Items);
        productClient.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task GetCartSnapshot_EmptyCart_ReturnsCartIdWithNoItems()
    {
        var (cartRepo, productClient) = MakeMocks();
        var userId = Guid.NewGuid();
        var cart = new Cart(userId);

        cartRepo.Setup(r => r.GetByUserIdWithItemsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);

        var svc = MakeSvc(cartRepo, productClient);
        var result = await svc.GetCartSnapshotAsync(userId);

        Assert.AreEqual(cart.Id, result.CartId);
        Assert.AreEqual(userId, result.UserId);
        Assert.IsEmpty(result.Items);
    }

    [TestMethod]
    public async Task GetCartSnapshot_WithItems_ReturnsItemIdsProductIdsAndQuantities()
    {
        var (cartRepo, productClient) = MakeMocks();
        var userId = Guid.NewGuid();
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();

        var cart = new Cart(userId);
        var itemA = cart.AddItem(productA, 2);
        var itemB = cart.AddItem(productB, 5);

        cartRepo.Setup(r => r.GetByUserIdWithItemsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);

        var svc = MakeSvc(cartRepo, productClient);
        var result = await svc.GetCartSnapshotAsync(userId);

        Assert.AreEqual(cart.Id, result.CartId);
        Assert.AreEqual(userId, result.UserId);
        Assert.HasCount(2, result.Items);
        CollectionAssert.AreEquivalent(
            new[]
            {
                new CartSnapshotItem(itemA.Id, productA, 2),
                new CartSnapshotItem(itemB.Id, productB, 5),
            },
            result.Items.ToList());
        productClient.VerifyNoOtherCalls();
        cartRepo.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- RemoveCheckedOutItems ---

    [TestMethod]
    public async Task RemoveCheckedOutItems_RemovesOnlyMatchingProductLines_SavesOnceAndReturnsCount()
    {
        var (cartRepo, productClient) = MakeMocks();
        var userId = Guid.NewGuid();
        var checkedOutA = Guid.NewGuid();
        var checkedOutB = Guid.NewGuid();
        var keptProduct = Guid.NewGuid();

        var cart = new Cart(userId);
        cart.AddItem(checkedOutA, 1);
        var kept = cart.AddItem(keptProduct, 4);
        cart.AddItem(checkedOutB, 2);

        cartRepo.Setup(r => r.GetByUserIdWithItemsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);

        var svc = MakeSvc(cartRepo, productClient);
        var removed = await svc.RemoveCheckedOutItemsAsync(userId, new[] { checkedOutA, checkedOutB, Guid.NewGuid() });

        Assert.AreEqual(2, removed);
        Assert.HasCount(1, cart.Items);
        Assert.AreEqual(kept.Id, cart.Items.Single().Id);
        Assert.AreEqual(keptProduct, cart.Items.Single().ProductId);
        Assert.AreEqual(4, cart.Items.Single().Quantity);
        cartRepo.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task RemoveCheckedOutItems_CalledTwice_SecondCallRemovesNothingAndDoesNotSave()
    {
        var (cartRepo, productClient) = MakeMocks();
        var userId = Guid.NewGuid();
        var checkedOut = Guid.NewGuid();

        var cart = new Cart(userId);
        cart.AddItem(checkedOut, 1);
        cart.AddItem(Guid.NewGuid(), 1);

        cartRepo.Setup(r => r.GetByUserIdWithItemsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);

        var svc = MakeSvc(cartRepo, productClient);
        var first = await svc.RemoveCheckedOutItemsAsync(userId, new[] { checkedOut });
        var second = await svc.RemoveCheckedOutItemsAsync(userId, new[] { checkedOut });

        Assert.AreEqual(1, first);
        Assert.AreEqual(0, second);
        Assert.HasCount(1, cart.Items);
        cartRepo.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task RemoveCheckedOutItems_NoMatchingLines_ReturnsZeroAndDoesNotSave()
    {
        var (cartRepo, productClient) = MakeMocks();
        var userId = Guid.NewGuid();

        var cart = new Cart(userId);
        cart.AddItem(Guid.NewGuid(), 3);

        cartRepo.Setup(r => r.GetByUserIdWithItemsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);

        var svc = MakeSvc(cartRepo, productClient);
        var removed = await svc.RemoveCheckedOutItemsAsync(userId, new[] { Guid.NewGuid() });

        Assert.AreEqual(0, removed);
        Assert.HasCount(1, cart.Items);
        cartRepo.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task RemoveCheckedOutItems_NoCart_ReturnsZeroAndDoesNotSave()
    {
        var (cartRepo, productClient) = MakeMocks();
        var svc = MakeSvc(cartRepo, productClient);

        var removed = await svc.RemoveCheckedOutItemsAsync(Guid.NewGuid(), new[] { Guid.NewGuid() });

        Assert.AreEqual(0, removed);
        cartRepo.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
