namespace ECom.Product.UnitTests;

using ECom.Product.Api.DTOs;
using ECom.Product.Api.Entities;
using ECom.Product.Api.Repositories;
using ECom.Product.Api.Services;
using ECom.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

[TestClass]
public sealed class StockServiceTests
{
    private sealed class Fixture
    {
        public Mock<IProductRepository> Repo { get; } = new();
        public Mock<IStockOperationRepository> Ops { get; } = new();
        public Mock<IProductService> ProductService { get; } = new();
        public List<ProcessedStockOperation> Staged { get; } = new();

        public Fixture(params Product[] products)
        {
            Repo.Setup(r => r.GetByIdsTrackedAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                    products.Where(p => ids.Contains(p.Id)).ToList().AsReadOnly());
            Repo.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            Ops.Setup(o => o.ExistsAsync(It.IsAny<Guid>(), It.IsAny<StockOperationType>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            Ops.Setup(o => o.Stage(It.IsAny<ProcessedStockOperation>()))
                .Callback<ProcessedStockOperation>(Staged.Add);

            ProductService.Setup(s => s.InvalidateProductsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        public void OperationExists(Guid orderId, StockOperationType operation) =>
            Ops.Setup(o => o.ExistsAsync(orderId, operation, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        public StockService CreateSvc() =>
            new(Repo.Object, Ops.Object, ProductService.Object, TimeProvider.System, NullLogger<StockService>.Instance);

        public void VerifyNothingPersisted()
        {
            Repo.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
            Ops.Verify(o => o.Stage(It.IsAny<ProcessedStockOperation>()), Times.Never);
            ProductService.Verify(s => s.InvalidateProductsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    private static Product MakeProduct(int stock, string name = "Widget") =>
        new(name, "A description", 9.99m, "Category", stock);

    private static StockLine[] Lines(params (Guid ProductId, int Quantity)[] lines) =>
        lines.Select(l => new StockLine(l.ProductId, l.Quantity)).ToArray();

    // --- Decrement: happy path ---

    [TestMethod]
    public async Task Decrement_ValidOrder_DecrementsStockStagesOperationAndSaves()
    {
        var p1 = MakeProduct(10);
        var p2 = MakeProduct(4);
        var f = new Fixture(p1, p2);
        var orderId = Guid.NewGuid();
        var before = DateTimeOffset.UtcNow;

        await f.CreateSvc().DecrementAsync(orderId, Lines((p1.Id, 3), (p2.Id, 4)));

        Assert.AreEqual(7, p1.StockQuantity);
        Assert.AreEqual(0, p2.StockQuantity);
        Assert.HasCount(1, f.Staged);
        Assert.AreEqual(orderId, f.Staged[0].OrderId);
        Assert.AreEqual(StockOperationType.Decrement, f.Staged[0].Operation);
        Assert.IsTrue(f.Staged[0].ProcessedAt >= before);
        f.Repo.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Decrement_InvalidatesCacheForAffectedProducts()
    {
        var p1 = MakeProduct(10);
        var p2 = MakeProduct(10);
        var f = new Fixture(p1, p2);
        IEnumerable<Guid>? invalidated = null;
        f.ProductService.Setup(s => s.InvalidateProductsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Guid>, CancellationToken>((ids, _) => invalidated = ids.ToList())
            .Returns(Task.CompletedTask);

        await f.CreateSvc().DecrementAsync(Guid.NewGuid(), Lines((p1.Id, 1), (p2.Id, 1), (p1.Id, 1)));

        Assert.IsNotNull(invalidated);
        CollectionAssert.AreEquivalent(new[] { p1.Id, p2.Id }, invalidated!.ToList());
    }

    [TestMethod]
    public async Task Decrement_DuplicateProductLines_AreAggregated()
    {
        var product = MakeProduct(5);
        var f = new Fixture(product);

        await f.CreateSvc().DecrementAsync(Guid.NewGuid(), Lines((product.Id, 2), (product.Id, 3)));

        Assert.AreEqual(0, product.StockQuantity);
    }

    [TestMethod]
    public async Task Decrement_DuplicateProductLinesExceedingStockInAggregate_ThrowsInsufficientStock()
    {
        // Each line alone fits (3 <= 4) but the aggregate (6) does not.
        var product = MakeProduct(4);
        var f = new Fixture(product);

        var ex = await Assert.ThrowsExactlyAsync<ConflictException>(() =>
            f.CreateSvc().DecrementAsync(Guid.NewGuid(), Lines((product.Id, 3), (product.Id, 3))));

        Assert.AreEqual("Insufficient stock", ex.ConflictTitle);
        Assert.AreEqual(4, product.StockQuantity);
        f.VerifyNothingPersisted();
    }

    // --- Decrement: idempotency ---

    [TestMethod]
    public async Task Decrement_AlreadyDecremented_IsNoOp()
    {
        var product = MakeProduct(10);
        var f = new Fixture(product);
        var orderId = Guid.NewGuid();
        f.OperationExists(orderId, StockOperationType.Decrement);

        await f.CreateSvc().DecrementAsync(orderId, Lines((product.Id, 3)));

        Assert.AreEqual(10, product.StockQuantity);
        f.Repo.Verify(r => r.GetByIdsTrackedAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
        f.VerifyNothingPersisted();
    }

    [TestMethod]
    public async Task Decrement_AfterRestoreTombstone_ThrowsOrderCompensatedConflict()
    {
        var product = MakeProduct(10);
        var f = new Fixture(product);
        var orderId = Guid.NewGuid();
        f.OperationExists(orderId, StockOperationType.Restore);

        var ex = await Assert.ThrowsExactlyAsync<ConflictException>(() =>
            f.CreateSvc().DecrementAsync(orderId, Lines((product.Id, 3))));

        Assert.AreEqual("Order compensated", ex.ConflictTitle);
        Assert.AreEqual(10, product.StockQuantity);
        f.VerifyNothingPersisted();
    }

    // --- Decrement: conflicts (all-or-nothing) ---

    [TestMethod]
    public async Task Decrement_UnknownProduct_ThrowsProductUnavailableConflict()
    {
        var existing = MakeProduct(10);
        var f = new Fixture(existing);

        var ex = await Assert.ThrowsExactlyAsync<ConflictException>(() =>
            f.CreateSvc().DecrementAsync(Guid.NewGuid(), Lines((existing.Id, 1), (Guid.NewGuid(), 1))));

        Assert.AreEqual("Product unavailable", ex.ConflictTitle);
        Assert.AreEqual(10, existing.StockQuantity);
        f.VerifyNothingPersisted();
    }

    [TestMethod]
    public async Task Decrement_InsufficientStock_ThrowsConflictAndChangesNoStock()
    {
        var plenty = MakeProduct(10);
        var scarce = MakeProduct(1);
        var f = new Fixture(plenty, scarce);

        var ex = await Assert.ThrowsExactlyAsync<ConflictException>(() =>
            f.CreateSvc().DecrementAsync(Guid.NewGuid(), Lines((plenty.Id, 2), (scarce.Id, 2))));

        Assert.AreEqual("Insufficient stock", ex.ConflictTitle);
        Assert.AreEqual(10, plenty.StockQuantity);
        Assert.AreEqual(1, scarce.StockQuantity);
        f.VerifyNothingPersisted();
    }

    // --- Decrement: input validation ---

    [TestMethod]
    public async Task Decrement_EmptyOrderId_ThrowsDomainException()
    {
        var f = new Fixture();
        await Assert.ThrowsExactlyAsync<DomainException>(() =>
            f.CreateSvc().DecrementAsync(Guid.Empty, Lines((Guid.NewGuid(), 1))));
        f.Ops.Verify(o => o.ExistsAsync(It.IsAny<Guid>(), It.IsAny<StockOperationType>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Decrement_NoItems_ThrowsDomainException()
    {
        var f = new Fixture();
        await Assert.ThrowsExactlyAsync<DomainException>(() =>
            f.CreateSvc().DecrementAsync(Guid.NewGuid(), Array.Empty<StockLine>()));
    }

    [TestMethod]
    public async Task Decrement_NullItems_ThrowsDomainException()
    {
        var f = new Fixture();
        await Assert.ThrowsExactlyAsync<DomainException>(() =>
            f.CreateSvc().DecrementAsync(Guid.NewGuid(), null!));
    }

    [TestMethod]
    public async Task Decrement_EmptyProductId_ThrowsDomainException()
    {
        var f = new Fixture();
        await Assert.ThrowsExactlyAsync<DomainException>(() =>
            f.CreateSvc().DecrementAsync(Guid.NewGuid(), Lines((Guid.Empty, 1))));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public async Task Decrement_NonPositiveQuantity_ThrowsDomainException(int quantity)
    {
        var product = MakeProduct(10);
        var f = new Fixture(product);
        await Assert.ThrowsExactlyAsync<DomainException>(() =>
            f.CreateSvc().DecrementAsync(Guid.NewGuid(), Lines((product.Id, quantity))));
        Assert.AreEqual(10, product.StockQuantity);
        f.VerifyNothingPersisted();
    }

    // --- Decrement: cache + persistence failures ---

    [TestMethod]
    public async Task Decrement_CacheInvalidationFailure_IsSwallowed()
    {
        var product = MakeProduct(10);
        var f = new Fixture(product);
        f.ProductService.Setup(s => s.InvalidateProductsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache unavailable"));

        await f.CreateSvc().DecrementAsync(Guid.NewGuid(), Lines((product.Id, 3)));

        Assert.AreEqual(7, product.StockQuantity);
        f.Repo.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Decrement_ConcurrentDuplicateDetectedOnSave_ReturnsWithoutInvalidating()
    {
        var product = MakeProduct(10);
        var f = new Fixture(product);
        var orderId = Guid.NewGuid();
        f.Ops.SetupSequence(o => o.ExistsAsync(orderId, StockOperationType.Decrement, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .ReturnsAsync(true);
        f.Repo.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("unique index violation"));

        await f.CreateSvc().DecrementAsync(orderId, Lines((product.Id, 3)));

        f.ProductService.Verify(s => s.InvalidateProductsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Decrement_SaveFailsWithoutRecordedOperation_Rethrows()
    {
        var product = MakeProduct(10);
        var f = new Fixture(product);
        f.Repo.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("some other failure"));

        await Assert.ThrowsExactlyAsync<DbUpdateException>(() =>
            f.CreateSvc().DecrementAsync(Guid.NewGuid(), Lines((product.Id, 3))));

        f.ProductService.Verify(s => s.InvalidateProductsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Decrement_ConcurrencyException_Propagates()
    {
        var product = MakeProduct(10);
        var f = new Fixture(product);
        var orderId = Guid.NewGuid();
        f.Repo.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("row version mismatch"));

        await Assert.ThrowsExactlyAsync<DbUpdateConcurrencyException>(() =>
            f.CreateSvc().DecrementAsync(orderId, Lines((product.Id, 3))));

        // Not treated as a duplicate: the idempotency record is not re-checked after the failure.
        f.Ops.Verify(o => o.ExistsAsync(orderId, StockOperationType.Decrement, It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- Restore ---

    [TestMethod]
    public async Task Restore_AfterDecrement_RestoresStockStagesOperationAndSaves()
    {
        var p1 = MakeProduct(7);
        var p2 = MakeProduct(0);
        var f = new Fixture(p1, p2);
        var orderId = Guid.NewGuid();
        f.OperationExists(orderId, StockOperationType.Decrement);

        await f.CreateSvc().RestoreAsync(orderId, Lines((p1.Id, 3), (p2.Id, 4)));

        Assert.AreEqual(10, p1.StockQuantity);
        Assert.AreEqual(4, p2.StockQuantity);
        Assert.HasCount(1, f.Staged);
        Assert.AreEqual(orderId, f.Staged[0].OrderId);
        Assert.AreEqual(StockOperationType.Restore, f.Staged[0].Operation);
        f.Repo.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
        f.ProductService.Verify(s => s.InvalidateProductsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Restore_DuplicateProductLines_AreAggregated()
    {
        var product = MakeProduct(0);
        var f = new Fixture(product);
        var orderId = Guid.NewGuid();
        f.OperationExists(orderId, StockOperationType.Decrement);

        await f.CreateSvc().RestoreAsync(orderId, Lines((product.Id, 2), (product.Id, 3)));

        Assert.AreEqual(5, product.StockQuantity);
    }

    [TestMethod]
    public async Task Restore_AlreadyRestored_IsNoOp()
    {
        var product = MakeProduct(5);
        var f = new Fixture(product);
        var orderId = Guid.NewGuid();
        f.OperationExists(orderId, StockOperationType.Decrement);
        f.OperationExists(orderId, StockOperationType.Restore);

        await f.CreateSvc().RestoreAsync(orderId, Lines((product.Id, 3)));

        Assert.AreEqual(5, product.StockQuantity);
        f.Repo.Verify(r => r.GetByIdsTrackedAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
        f.VerifyNothingPersisted();
    }

    [TestMethod]
    public async Task Restore_WithoutPriorDecrement_RecordsTombstoneAndChangesNoStock()
    {
        var product = MakeProduct(5);
        var f = new Fixture(product);
        var orderId = Guid.NewGuid();

        await f.CreateSvc().RestoreAsync(orderId, Lines((product.Id, 3)));

        Assert.AreEqual(5, product.StockQuantity);
        Assert.HasCount(1, f.Staged);
        Assert.AreEqual(orderId, f.Staged[0].OrderId);
        Assert.AreEqual(StockOperationType.Restore, f.Staged[0].Operation);
        f.Repo.Verify(r => r.GetByIdsTrackedAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
        f.Repo.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
        f.ProductService.Verify(s => s.InvalidateProductsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Restore_ProductNoLongerExists_IsSkippedAndOthersRestored()
    {
        var product = MakeProduct(2);
        var f = new Fixture(product);
        var orderId = Guid.NewGuid();
        f.OperationExists(orderId, StockOperationType.Decrement);

        await f.CreateSvc().RestoreAsync(orderId, Lines((product.Id, 3), (Guid.NewGuid(), 1)));

        Assert.AreEqual(5, product.StockQuantity);
        f.Repo.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Restore_EmptyOrderId_ThrowsDomainException()
    {
        var f = new Fixture();
        await Assert.ThrowsExactlyAsync<DomainException>(() =>
            f.CreateSvc().RestoreAsync(Guid.Empty, Lines((Guid.NewGuid(), 1))));
        f.VerifyNothingPersisted();
    }

    [TestMethod]
    public async Task Restore_NoItems_ThrowsDomainException()
    {
        var f = new Fixture();
        await Assert.ThrowsExactlyAsync<DomainException>(() =>
            f.CreateSvc().RestoreAsync(Guid.NewGuid(), Array.Empty<StockLine>()));
        f.VerifyNothingPersisted();
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-5)]
    public async Task Restore_NonPositiveQuantity_ThrowsDomainException(int quantity)
    {
        var f = new Fixture();
        await Assert.ThrowsExactlyAsync<DomainException>(() =>
            f.CreateSvc().RestoreAsync(Guid.NewGuid(), Lines((Guid.NewGuid(), quantity))));
        f.VerifyNothingPersisted();
    }

    [TestMethod]
    public async Task Restore_CacheInvalidationFailure_IsSwallowed()
    {
        var product = MakeProduct(0);
        var f = new Fixture(product);
        var orderId = Guid.NewGuid();
        f.OperationExists(orderId, StockOperationType.Decrement);
        f.ProductService.Setup(s => s.InvalidateProductsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache unavailable"));

        await f.CreateSvc().RestoreAsync(orderId, Lines((product.Id, 3)));

        Assert.AreEqual(3, product.StockQuantity);
    }

    [TestMethod]
    public async Task Restore_ConcurrentDuplicateDetectedOnSave_ReturnsWithoutInvalidating()
    {
        var product = MakeProduct(0);
        var f = new Fixture(product);
        var orderId = Guid.NewGuid();
        f.OperationExists(orderId, StockOperationType.Decrement);
        f.Ops.SetupSequence(o => o.ExistsAsync(orderId, StockOperationType.Restore, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .ReturnsAsync(true);
        f.Repo.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("unique index violation"));

        await f.CreateSvc().RestoreAsync(orderId, Lines((product.Id, 3)));

        f.ProductService.Verify(s => s.InvalidateProductsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
