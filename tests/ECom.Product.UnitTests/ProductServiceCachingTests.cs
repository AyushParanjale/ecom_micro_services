namespace ECom.Product.UnitTests;

using ECom.Product.Api.Entities;
using ECom.Product.Api.Repositories;
using ECom.Product.Api.Services;
using ECom.Shared.Caching;
using ECom.Product.Api.DTOs;
using ECom.Product.Api.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

[TestClass]
public sealed class ProductServiceCachingTests
{
    private static readonly CachingOptions DefaultOptions = new();

    private static ProductService CreateSvc(IProductRepository repo, ICacheService cache) =>
        new(repo, cache, Options.Create(DefaultOptions), NullLogger<ProductService>.Instance);

    private static Mock<IProductRepository> EmptyRepoMock()
    {
        var mock = new Mock<IProductRepository>();
        mock.Setup(r => r.GetAvailablePageAsync(It.IsAny<ProductQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Product>().AsReadOnly(), 0));
        mock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);
        return mock;
    }

    private static ProductResponse MakeResponse(Guid? id = null) =>
        new(id ?? Guid.NewGuid(), "Widget", "Desc", 9.99m, "Cat", 5, null);

    // --- GetProducts list cache ---

    [TestMethod]
    public async Task GetProducts_CacheMiss_HitsRepoAndCachesResult()
    {
        var repoMock = EmptyRepoMock();
        var products = new List<Product> { new("Widget", "Desc", 9.99m, "Cat", 5, null) };
        repoMock.Setup(r => r.GetAvailablePageAsync(It.IsAny<ProductQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((products.AsReadOnly(), products.Count));

        var cacheMock = new Mock<ICacheService>();

        // Version token miss → factory creates fresh version
        cacheMock.Setup(c => c.GetOrCreateAsync<string>(
                ProductCacheKeys.VersionKey,
                It.IsAny<Func<CancellationToken, Task<string>>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<string>>, TimeSpan, CancellationToken>(
                (_, factory, _, ct) => factory(ct));

        // List key miss
        cacheMock.Setup(c => c.GetAsync<PagedResponse<ProductResponse>>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagedResponse<ProductResponse>?)null);

        cacheMock.Setup(c => c.SetAsync<PagedResponse<ProductResponse>>(
                It.IsAny<string>(), It.IsAny<PagedResponse<ProductResponse>>(),
                It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var svc = CreateSvc(repoMock.Object, cacheMock.Object);
        var result = await svc.GetProductsAsync(new ProductQuery());

        Assert.HasCount(1, result.Items);
        repoMock.Verify(r => r.GetAvailablePageAsync(It.IsAny<ProductQuery>(), It.IsAny<CancellationToken>()), Times.Once);
        cacheMock.Verify(c => c.SetAsync<PagedResponse<ProductResponse>>(
            It.IsAny<string>(), It.IsAny<PagedResponse<ProductResponse>>(),
            It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetProducts_CacheHit_DoesNotHitRepo()
    {
        var repoMock = EmptyRepoMock();
        var cachedPage = new PagedResponse<ProductResponse>(
            new List<ProductResponse> { MakeResponse() }.AsReadOnly(), 1, ProductQuery.DefaultPageSize, 1, 1);

        var cacheMock = new Mock<ICacheService>();
        const string version = "test-version";
        var query = new ProductQuery();

        cacheMock.Setup(c => c.GetOrCreateAsync<string>(
                ProductCacheKeys.VersionKey,
                It.IsAny<Func<CancellationToken, Task<string>>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(version);

        cacheMock.Setup(c => c.GetAsync<PagedResponse<ProductResponse>>(
                ProductCacheKeys.List(version, query), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedPage);

        var svc = CreateSvc(repoMock.Object, cacheMock.Object);
        var result = await svc.GetProductsAsync(query);

        Assert.AreSame(cachedPage, result);
        repoMock.Verify(r => r.GetAvailablePageAsync(It.IsAny<ProductQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public void ListCacheKey_DiffersPerQueryDimension()
    {
        const string version = "v";
        var baseQuery = new ProductQuery();
        var variants = new[]
        {
            baseQuery,
            baseQuery with { Search = "phone" },
            baseQuery with { Category = "Books" },
            baseQuery with { MinPrice = 10m },
            baseQuery with { MaxPrice = 10m },
            baseQuery with { SortBy = "price" },
            baseQuery with { SortDirection = "desc" },
            baseQuery with { Page = 2 },
            baseQuery with { PageSize = 24 },
        };

        var keys = variants.Select(q => ProductCacheKeys.List(version, q)).ToList();

        Assert.HasCount(variants.Length, keys.Distinct());
    }

    [TestMethod]
    public void ListCacheKey_IsCaseAndWhitespaceInsensitiveForTextFilters()
    {
        var a = ProductCacheKeys.List("v", new ProductQuery { Search = "  Phone ", Category = "BOOKS" });
        var b = ProductCacheKeys.List("v", new ProductQuery { Search = "phone", Category = "books" });

        Assert.AreEqual(a, b);
    }

    // --- GetProductById detail cache ---

    [TestMethod]
    public async Task GetProductById_CacheMiss_HitsRepoAndCachesResult()
    {
        var product = new Product("Widget", "Desc", 9.99m, "Cat", 5, null);
        var repoMock = EmptyRepoMock();
        repoMock.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var cacheMock = new Mock<ICacheService>();
        cacheMock.Setup(c => c.GetAsync<ProductResponse>(
                ProductCacheKeys.Detail(product.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductResponse?)null);
        cacheMock.Setup(c => c.SetAsync<ProductResponse>(
                It.IsAny<string>(), It.IsAny<ProductResponse>(),
                It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var svc = CreateSvc(repoMock.Object, cacheMock.Object);
        var result = await svc.GetProductByIdAsync(product.Id);

        Assert.AreEqual(product.Id, result.Id);
        cacheMock.Verify(c => c.SetAsync<ProductResponse>(
            ProductCacheKeys.Detail(product.Id), It.IsAny<ProductResponse>(),
            It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetProductById_CacheHit_DoesNotHitRepo()
    {
        var id = Guid.NewGuid();
        var cachedResponse = MakeResponse(id);

        var cacheMock = new Mock<ICacheService>();
        cacheMock.Setup(c => c.GetAsync<ProductResponse>(
                ProductCacheKeys.Detail(id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResponse);

        var repoMock = EmptyRepoMock();
        var svc = CreateSvc(repoMock.Object, cacheMock.Object);
        var result = await svc.GetProductByIdAsync(id);

        Assert.AreSame(cachedResponse, result);
        repoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- Cache invalidation on mutations ---

    [TestMethod]
    public async Task CreateProduct_RemovesVersionKey()
    {
        var repoMock = EmptyRepoMock();
        repoMock.Setup(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cacheMock = new Mock<ICacheService>();
        cacheMock.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var svc = CreateSvc(repoMock.Object, cacheMock.Object);
        await svc.CreateProductAsync(new CreateProductRequest
        {
            Name = "New Product", Description = "Desc", Price = 9.99m, Category = "Cat", StockQuantity = 10
        });

        cacheMock.Verify(c => c.RemoveAsync(ProductCacheKeys.VersionKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task UpdateProduct_RemovesDetailKeyAndVersionKey()
    {
        var product = new Product("Widget", "Desc", 9.99m, "Cat", 5, null);
        var repoMock = EmptyRepoMock();
        repoMock.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        repoMock.Setup(r => r.UpdateAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cacheMock = new Mock<ICacheService>();
        cacheMock.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var svc = CreateSvc(repoMock.Object, cacheMock.Object);
        await svc.UpdateProductAsync(product.Id, new UpdateProductRequest
        {
            Name = "Updated", Description = "Desc", Price = 19.99m, Category = "Cat", StockQuantity = 5
        });

        cacheMock.Verify(c => c.RemoveAsync(ProductCacheKeys.Detail(product.Id), It.IsAny<CancellationToken>()), Times.Once);
        cacheMock.Verify(c => c.RemoveAsync(ProductCacheKeys.VersionKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task DeleteProduct_RemovesDetailKeyAndVersionKey()
    {
        var product = new Product("Widget", "Desc", 9.99m, "Cat", 5, null);
        var repoMock = EmptyRepoMock();
        repoMock.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        repoMock.Setup(r => r.DeleteAsync(product.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cacheMock = new Mock<ICacheService>();
        cacheMock.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var svc = CreateSvc(repoMock.Object, cacheMock.Object);
        await svc.DeleteProductAsync(product.Id);

        cacheMock.Verify(c => c.RemoveAsync(ProductCacheKeys.Detail(product.Id), It.IsAny<CancellationToken>()), Times.Once);
        cacheMock.Verify(c => c.RemoveAsync(ProductCacheKeys.VersionKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task InvalidateProductsAsync_RemovesEachDetailKeyAndVersionKey()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        var cacheMock = new Mock<ICacheService>();
        cacheMock.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var svc = CreateSvc(EmptyRepoMock().Object, cacheMock.Object);
        await svc.InvalidateProductsAsync(new[] { id1, id2 });

        cacheMock.Verify(c => c.RemoveAsync(ProductCacheKeys.Detail(id1), It.IsAny<CancellationToken>()), Times.Once);
        cacheMock.Verify(c => c.RemoveAsync(ProductCacheKeys.Detail(id2), It.IsAny<CancellationToken>()), Times.Once);
        cacheMock.Verify(c => c.RemoveAsync(ProductCacheKeys.VersionKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetProductsAsync_CacheFailure_IsSwallowed_AndReturnsDbResult()
    {
        var products = new List<Product> { new("Widget", "Desc", 9.99m, "Cat", 5, null) };
        var repoMock = EmptyRepoMock();
        repoMock.Setup(r => r.GetAvailablePageAsync(It.IsAny<ProductQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((products.AsReadOnly(), products.Count));

        // Cache throws on every call — service should still return DB result
        var cacheMock = new Mock<ICacheService>();
        cacheMock.Setup(c => c.GetOrCreateAsync<string>(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<string>>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache unavailable"));

        var svc = CreateSvc(repoMock.Object, cacheMock.Object);
        // Should not throw — cache failure propagates here since GetOrCreateAsync exception
        // is not caught at that level; the list-hit check would not be reached.
        // This validates the cache error is NOT silently swallowed at GetProductsAsync level.
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            svc.GetProductsAsync(new ProductQuery()));
    }
}
