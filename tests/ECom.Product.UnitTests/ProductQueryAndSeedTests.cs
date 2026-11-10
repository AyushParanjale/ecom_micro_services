namespace ECom.Product.UnitTests;

using System.ComponentModel.DataAnnotations;
using ECom.Product.Api.DTOs;
using ECom.Product.Api.Services;

[TestClass]
public sealed class ProductQueryAndSeedTests
{
    private static List<ValidationResult> Validate(ProductQuery query)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(query, new ValidationContext(query), results, validateAllProperties: true);
        return results;
    }

    [TestMethod]
    public void ProductQuery_Defaults_AreValid()
    {
        var query = new ProductQuery();

        Assert.IsEmpty(Validate(query));
        Assert.AreEqual(1, query.Page);
        Assert.AreEqual(ProductQuery.DefaultPageSize, query.PageSize);
        Assert.IsFalse(query.SortByPrice);
        Assert.IsFalse(query.SortDescending);
    }

    [TestMethod]
    public void ProductQuery_SortValues_AreCaseInsensitive()
    {
        var query = new ProductQuery { SortBy = "PRICE", SortDirection = "Desc" };

        Assert.IsEmpty(Validate(query));
        Assert.IsTrue(query.SortByPrice);
        Assert.IsTrue(query.SortDescending);
    }

    [TestMethod]
    [DataRow(0, 12)]
    [DataRow(1, 0)]
    [DataRow(1, 101)]
    public void ProductQuery_OutOfRangePaging_IsInvalid(int page, int pageSize)
    {
        Assert.IsNotEmpty(Validate(new ProductQuery { Page = page, PageSize = pageSize }));
    }

    [TestMethod]
    [DataRow("rating", null)]
    [DataRow(null, "up")]
    public void ProductQuery_UnknownSort_IsInvalid(string? sortBy, string? sortDirection)
    {
        Assert.IsNotEmpty(Validate(new ProductQuery { SortBy = sortBy, SortDirection = sortDirection }));
    }

    [TestMethod]
    public void ProductQuery_MinPriceAboveMaxPrice_IsInvalid()
    {
        Assert.IsNotEmpty(Validate(new ProductQuery { MinPrice = 50m, MaxPrice = 10m }));
    }

    [TestMethod]
    public void ProductQuery_NegativePrice_IsInvalid()
    {
        Assert.IsNotEmpty(Validate(new ProductQuery { MinPrice = -1m }));
    }

    [TestMethod]
    public void ProductQuery_OverlongSearch_IsInvalid()
    {
        Assert.IsNotEmpty(Validate(new ProductQuery { Search = new string('a', 101) }));
    }

    [TestMethod]
    public void SeedCatalogue_NamesAreUnique()
    {
        var names = ProductSeedService.SeedCatalogue().Select(p => p.Name).ToList();

        Assert.HasCount(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void SeedCatalogue_EveryProductHasAValidHttpsImageUrl()
    {
        foreach (var product in ProductSeedService.SeedCatalogue())
        {
            Assert.IsNotNull(product.ImageUrl, product.Name);
            Assert.IsTrue(
                Uri.TryCreate(product.ImageUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps,
                $"{product.Name}: {product.ImageUrl}");
            Assert.IsLessThanOrEqualTo(500, product.ImageUrl!.Length, product.Name);
        }
    }

    [TestMethod]
    public void SeedCatalogue_FitsColumnLimits()
    {
        foreach (var product in ProductSeedService.SeedCatalogue())
        {
            Assert.IsLessThanOrEqualTo(200, product.Name.Length, product.Name);
            Assert.IsLessThanOrEqualTo(2000, product.Description.Length, product.Name);
            Assert.IsLessThanOrEqualTo(100, product.Category.Length, product.Name);
            Assert.IsGreaterThan(0, product.StockQuantity, product.Name);
        }
    }
}
