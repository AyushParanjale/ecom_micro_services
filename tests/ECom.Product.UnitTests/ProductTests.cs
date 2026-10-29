namespace ECom.Product.UnitTests;

using ECom.Product.Api.Entities;
using ECom.Shared.Exceptions;

[TestClass]
public sealed class ProductTests
{
    [TestMethod]
    public void CreateProduct_ValidArguments_Succeeds()
    {
        var product = new Product("Laptop", "A fast laptop", 999.99m, "Electronics", 10);

        Assert.AreEqual("Laptop", product.Name);
        Assert.AreEqual("A fast laptop", product.Description);
        Assert.AreEqual(999.99m, product.Price);
        Assert.AreEqual("Electronics", product.Category);
        Assert.AreEqual(10, product.StockQuantity);
        Assert.AreNotEqual(Guid.Empty, product.Id);
    }

    [TestMethod]
    public void CreateProduct_TrimsWhitespace()
    {
        var product = new Product("  Laptop  ", "  Desc  ", 9.99m, "  Books  ", 1);

        Assert.AreEqual("Laptop", product.Name);
        Assert.AreEqual("Desc", product.Description);
        Assert.AreEqual("Books", product.Category);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void CreateProduct_EmptyName_ThrowsDomainException(string name)
    {
        Assert.ThrowsExactly<DomainException>(() =>
            new Product(name, "Description", 10m, "Category", 1));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void CreateProduct_EmptyCategory_ThrowsDomainException(string category)
    {
        Assert.ThrowsExactly<DomainException>(() =>
            new Product("Name", "Description", 10m, category, 1));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void CreateProduct_EmptyDescription_ThrowsDomainException(string description)
    {
        Assert.ThrowsExactly<DomainException>(() =>
            new Product("Name", description, 10m, "Category", 1));
    }

    [TestMethod]
    public void CreateProduct_ZeroPrice_ThrowsDomainException()
    {
        Assert.ThrowsExactly<DomainException>(() =>
            new Product("Name", "Desc", 0m, "Category", 1));
    }

    [TestMethod]
    public void CreateProduct_NegativePrice_ThrowsDomainException()
    {
        Assert.ThrowsExactly<DomainException>(() =>
            new Product("Name", "Desc", -0.01m, "Category", 1));
    }

    [TestMethod]
    public void CreateProduct_NegativeStock_ThrowsDomainException()
    {
        Assert.ThrowsExactly<DomainException>(() =>
            new Product("Name", "Desc", 10m, "Category", -1));
    }

    [TestMethod]
    public void CreateProduct_ZeroStock_Succeeds()
    {
        var product = new Product("Name", "Desc", 10m, "Category", 0);

        Assert.AreEqual(0, product.StockQuantity);
    }

    [TestMethod]
    public void DecrementStock_ValidAmount_ReducesStock()
    {
        var product = new Product("Name", "Desc", 10m, "Category", 5);

        product.DecrementStock(3);

        Assert.AreEqual(2, product.StockQuantity);
    }

    [TestMethod]
    public void DecrementStock_ExactStock_ReachesZero()
    {
        var product = new Product("Name", "Desc", 10m, "Category", 5);

        product.DecrementStock(5);

        Assert.AreEqual(0, product.StockQuantity);
    }

    [TestMethod]
    public void DecrementStock_BelowZero_ThrowsDomainException()
    {
        var product = new Product("Name", "Desc", 10m, "Category", 3);

        Assert.ThrowsExactly<DomainException>(() => product.DecrementStock(4));
    }

    [TestMethod]
    public void DecrementStock_ZeroQuantity_ThrowsDomainException()
    {
        var product = new Product("Name", "Desc", 10m, "Category", 3);

        Assert.ThrowsExactly<DomainException>(() => product.DecrementStock(0));
    }

    [TestMethod]
    public void UpdateDetails_ValidArguments_UpdatesProperties()
    {
        var product = new Product("Old Name", "Old Desc", 9.99m, "OldCat", 5);

        product.UpdateDetails("New Name", "New Desc", 19.99m, "NewCat", "http://img.example/img.png");

        Assert.AreEqual("New Name", product.Name);
        Assert.AreEqual("New Desc", product.Description);
        Assert.AreEqual(19.99m, product.Price);
        Assert.AreEqual("NewCat", product.Category);
        Assert.AreEqual("http://img.example/img.png", product.ImageUrl);
    }

    [TestMethod]
    public void UpdateDetails_EmptyName_ThrowsDomainException()
    {
        var product = new Product("Name", "Desc", 10m, "Category", 5);

        Assert.ThrowsExactly<DomainException>(() =>
            product.UpdateDetails("", "New Desc", 10m, "Category"));
    }

    [TestMethod]
    public void UpdateDetails_ZeroPrice_ThrowsDomainException()
    {
        var product = new Product("Name", "Desc", 10m, "Category", 5);

        Assert.ThrowsExactly<DomainException>(() =>
            product.UpdateDetails("Name", "Desc", 0m, "Category"));
    }
}
