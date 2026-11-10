namespace ECom.Product.UnitTests;

using ECom.Product.Api.Entities;
using ECom.Shared.Exceptions;
using ECom.Product.Api.Repositories;
using ECom.Product.Api.Services;
using ECom.Product.Api.DTOs;
using ECom.Product.Api.Options;
using ECom.Shared.Caching;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

[TestClass]
public sealed class ProductServiceTests
{
    // Always-miss cache so domain invariant tests are unaffected by caching behaviour.
    private sealed class PassThroughCacheService : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
            => Task.FromResult<T?>(null);
        public Task SetAsync<T>(string key, T value, TimeSpan expiry, CancellationToken ct = default) where T : class
            => Task.CompletedTask;
        public Task RemoveAsync(string key, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan expiry, CancellationToken ct = default) where T : class
            => factory(ct);
    }

    private static ProductService CreateSvc(IProductRepository repo) =>
        new(repo,
            new PassThroughCacheService(),
            Options.Create(new CachingOptions()),
            NullLogger<ProductService>.Instance);

    private static Mock<IProductRepository> EmptyRepoMock()
    {
        var mock = new Mock<IProductRepository>();
        mock.Setup(r => r.GetAvailablePageAsync(It.IsAny<ProductQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Product>().AsReadOnly(), 0));
        mock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);
        return mock;
    }

    private static Product MakeProduct(string name = "Widget", int stock = 10) =>
        new(name, "A description", 9.99m, "Category", stock, null);

    private static CreateProductRequest ValidCreate(int stock = 10) => new()
    {
        Name = "Widget",
        Description = "A description",
        Price = 9.99m,
        Category = "Category",
        StockQuantity = stock,
    };

    private static UpdateProductRequest ValidUpdate(int stock = 10) => new()
    {
        Name = "Updated Widget",
        Description = "Updated description",
        Price = 19.99m,
        Category = "Updated Category",
        StockQuantity = stock,
    };

    // --- Create ---

    [TestMethod]
    public async Task Create_ValidRequest_ReturnsProductResponse()
    {
        var mock = EmptyRepoMock();
        mock.Setup(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var svc = CreateSvc(mock.Object);
        var result = await svc.CreateProductAsync(ValidCreate());

        Assert.AreEqual("Widget", result.Name);
        Assert.AreEqual(9.99m, result.Price);
    }

    [TestMethod]
    public async Task Create_InputIsTrimmed()
    {
        var mock = EmptyRepoMock();
        Product? captured = null;
        mock.Setup(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Callback<Product, CancellationToken>((p, _) => captured = p)
            .Returns(Task.CompletedTask);

        var svc = CreateSvc(mock.Object);
        await svc.CreateProductAsync(new CreateProductRequest
        {
            Name = "  Widget  ",
            Description = "  Desc  ",
            Price = 9.99m,
            Category = "  Cat  ",
            StockQuantity = 5,
        });

        Assert.IsNotNull(captured);
        Assert.AreEqual("Widget", captured!.Name);
        Assert.AreEqual("Cat", captured.Category);
    }

    [TestMethod]
    public async Task Create_ZeroPrice_ThrowsDomainException()
    {
        var svc = CreateSvc(EmptyRepoMock().Object);
        await Assert.ThrowsExactlyAsync<DomainException>(() =>
            svc.CreateProductAsync(ValidCreate() with { Price = 0m }));
    }

    [TestMethod]
    public async Task Create_NegativeStock_ThrowsDomainException()
    {
        var svc = CreateSvc(EmptyRepoMock().Object);
        await Assert.ThrowsExactlyAsync<DomainException>(() =>
            svc.CreateProductAsync(ValidCreate(-1)));
    }

    [TestMethod]
    public async Task Create_EmptyName_ThrowsDomainException()
    {
        var svc = CreateSvc(EmptyRepoMock().Object);
        await Assert.ThrowsExactlyAsync<DomainException>(() =>
            svc.CreateProductAsync(ValidCreate() with { Name = "   " }));
    }

    // --- GetProducts ---

    [TestMethod]
    public async Task GetProducts_ReturnsOnlyInStockProducts()
    {
        var mock = EmptyRepoMock();
        mock.Setup(r => r.GetAvailablePageAsync(It.IsAny<ProductQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Product> { MakeProduct(stock: 5) }.AsReadOnly(), 1));

        var svc = CreateSvc(mock.Object);
        var result = await svc.GetProductsAsync(new ProductQuery());

        Assert.HasCount(1, result.Items);
        Assert.AreEqual(1, result.TotalCount);
    }

    [TestMethod]
    public async Task GetProducts_NoMatches_ReturnsEmptyPage()
    {
        var svc = CreateSvc(EmptyRepoMock().Object);
        var result = await svc.GetProductsAsync(new ProductQuery());
        Assert.IsEmpty(result.Items);
        Assert.AreEqual(0, result.TotalCount);
        Assert.AreEqual(0, result.TotalPages);
    }

    [TestMethod]
    public async Task GetProducts_PassesQueryToRepo()
    {
        var mock = EmptyRepoMock();
        ProductQuery? captured = null;
        mock.Setup(r => r.GetAvailablePageAsync(It.IsAny<ProductQuery>(), It.IsAny<CancellationToken>()))
            .Callback<ProductQuery, CancellationToken>((q, _) => captured = q)
            .ReturnsAsync((new List<Product>().AsReadOnly(), 0));

        var query = new ProductQuery { Category = "Electronics", Search = "phone", Page = 2, PageSize = 5 };
        var svc = CreateSvc(mock.Object);
        await svc.GetProductsAsync(query);

        Assert.AreSame(query, captured);
    }

    [TestMethod]
    public async Task GetProducts_ComputesPagingMetadata()
    {
        var mock = EmptyRepoMock();
        mock.Setup(r => r.GetAvailablePageAsync(It.IsAny<ProductQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Product> { MakeProduct("A"), MakeProduct("B") }.AsReadOnly(), 12));

        var svc = CreateSvc(mock.Object);
        var result = await svc.GetProductsAsync(new ProductQuery { Page = 3, PageSize = 5 });

        Assert.AreEqual(3, result.Page);
        Assert.AreEqual(5, result.PageSize);
        Assert.AreEqual(12, result.TotalCount);
        Assert.AreEqual(3, result.TotalPages);
        Assert.HasCount(2, result.Items);
    }

    [TestMethod]
    public async Task GetCategories_ReturnsRepoCategories()
    {
        var mock = EmptyRepoMock();
        mock.Setup(r => r.GetAvailableCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Books", "Electronics" }.AsReadOnly());

        var svc = CreateSvc(mock.Object);
        var result = await svc.GetCategoriesAsync();

        CollectionAssert.AreEqual(new[] { "Books", "Electronics" }, result.ToArray());
    }

    // --- GetProductById ---

    [TestMethod]
    public async Task GetProductById_InStockProduct_ReturnsResponse()
    {
        var product = MakeProduct(stock: 5);
        var mock = EmptyRepoMock();
        mock.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var svc = CreateSvc(mock.Object);
        var result = await svc.GetProductByIdAsync(product.Id);

        Assert.AreEqual(product.Id, result.Id);
    }

    [TestMethod]
    public async Task GetProductById_ZeroStockProduct_ThrowsNotFoundException()
    {
        var product = MakeProduct(stock: 0);
        var mock = EmptyRepoMock();
        mock.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var svc = CreateSvc(mock.Object);
        await Assert.ThrowsExactlyAsync<NotFoundException>(() =>
            svc.GetProductByIdAsync(product.Id));
    }

    [TestMethod]
    public async Task GetProductById_UnknownProduct_ThrowsNotFoundException()
    {
        var svc = CreateSvc(EmptyRepoMock().Object);
        await Assert.ThrowsExactlyAsync<NotFoundException>(() =>
            svc.GetProductByIdAsync(Guid.NewGuid()));
    }

    // --- GetProductsByIds (internal batch lookup) ---

    [TestMethod]
    public async Task GetProductsByIds_ReturnsAllExistingProductsRegardlessOfStock()
    {
        var inStock = MakeProduct("In Stock", stock: 5);
        var outOfStock = MakeProduct("Out Of Stock", stock: 0);
        var mock = EmptyRepoMock();
        IEnumerable<Guid>? capturedIds = null;
        mock.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Guid>, CancellationToken>((ids, _) => capturedIds = ids.ToList())
            .ReturnsAsync(new List<Product> { inStock, outOfStock }.AsReadOnly());

        var svc = CreateSvc(mock.Object);
        var result = await svc.GetProductsByIdsAsync(new[] { inStock.Id, outOfStock.Id, inStock.Id, Guid.NewGuid() });

        Assert.HasCount(2, result);
        Assert.IsTrue(result.Any(r => r.Id == outOfStock.Id && r.StockQuantity == 0));
        Assert.IsNotNull(capturedIds);
        Assert.HasCount(3, capturedIds!);
        mock.Verify(r => r.GetAvailablePageAsync(It.IsAny<ProductQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- Update ---

    [TestMethod]
    public async Task Update_ExistingProduct_ReturnsUpdatedResponse()
    {
        var product = MakeProduct();
        var mock = EmptyRepoMock();
        mock.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        mock.Setup(r => r.UpdateAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var svc = CreateSvc(mock.Object);
        var result = await svc.UpdateProductAsync(product.Id, ValidUpdate());

        Assert.AreEqual("Updated Widget", result.Name);
        Assert.AreEqual(19.99m, result.Price);
    }

    [TestMethod]
    public async Task Update_UnknownProduct_ThrowsNotFoundException()
    {
        var svc = CreateSvc(EmptyRepoMock().Object);
        await Assert.ThrowsExactlyAsync<NotFoundException>(() =>
            svc.UpdateProductAsync(Guid.NewGuid(), ValidUpdate()));
    }

    [TestMethod]
    public async Task Update_InvalidPrice_ThrowsDomainException()
    {
        var product = MakeProduct();
        var mock = EmptyRepoMock();
        mock.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var svc = CreateSvc(mock.Object);
        await Assert.ThrowsExactlyAsync<DomainException>(() =>
            svc.UpdateProductAsync(product.Id, ValidUpdate() with { Price = 0m }));
    }

    [TestMethod]
    public async Task Update_NegativeStock_ThrowsDomainException()
    {
        var product = MakeProduct();
        var mock = EmptyRepoMock();
        mock.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var svc = CreateSvc(mock.Object);
        await Assert.ThrowsExactlyAsync<DomainException>(() =>
            svc.UpdateProductAsync(product.Id, ValidUpdate(-1)));
    }

    [TestMethod]
    public async Task Update_OutOfStockProduct_CanStillBeUpdated()
    {
        var product = MakeProduct(stock: 0);
        var mock = EmptyRepoMock();
        mock.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        mock.Setup(r => r.UpdateAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var svc = CreateSvc(mock.Object);
        var result = await svc.UpdateProductAsync(product.Id, ValidUpdate(5));

        Assert.AreEqual(5, result.StockQuantity);
    }

    // --- Delete ---

    [TestMethod]
    public async Task Delete_ExistingProduct_CallsRepoDelete()
    {
        var product = MakeProduct();
        var mock = EmptyRepoMock();
        mock.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        mock.Setup(r => r.DeleteAsync(product.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var svc = CreateSvc(mock.Object);
        await svc.DeleteProductAsync(product.Id);

        mock.Verify(r => r.DeleteAsync(product.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Delete_UnknownProduct_ThrowsNotFoundException()
    {
        var svc = CreateSvc(EmptyRepoMock().Object);
        await Assert.ThrowsExactlyAsync<NotFoundException>(() =>
            svc.DeleteProductAsync(Guid.NewGuid()));
    }
}
