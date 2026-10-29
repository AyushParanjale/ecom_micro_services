namespace ECom.Product.Api.Services;

using ECom.Product.Api.Data;
using ECom.Product.Api.Entities;
using Microsoft.EntityFrameworkCore;

public sealed class ProductSeedService
{
    private readonly ProductDbContext _db;
    private readonly ILogger<ProductSeedService> _logger;

    public ProductSeedService(ProductDbContext db, ILogger<ProductSeedService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedProductsAsync()
    {
        if (await _db.Products.AnyAsync())
        {
            _logger.LogInformation("Product seed skipped: products already exist.");
            return;
        }

        var products = new List<Product>
        {
            // Electronics
            new("Wireless Noise-Cancelling Headphones",
                "Over-ear headphones with active noise cancellation, 30-hour battery life, and foldable design.",
                149.99m, "Electronics", 42),
            new("Mechanical Keyboard",
                "Tenkeyless mechanical keyboard with Cherry MX Brown switches, RGB backlighting, and USB-C cable.",
                89.99m, "Electronics", 30),
            new("4K Webcam",
                "Ultra-HD webcam with autofocus, built-in ring light, and wide-angle 90° lens for streaming and video calls.",
                74.99m, "Electronics", 55),
            new("Portable Bluetooth Speaker",
                "IP67 waterproof speaker, 360° surround sound, 24-hour battery, and USB-C charging.",
                59.99m, "Electronics", 80),
            new("USB-C Hub 7-in-1",
                "Multiport adapter with 4K HDMI, 2× USB-A, SD card reader, microSD, and 100W PD pass-through.",
                39.99m, "Electronics", 120),

            // Clothing
            new("Classic Crew-Neck T-Shirt",
                "100% organic cotton heavyweight tee, pre-shrunk, available in a range of neutral colors.",
                24.99m, "Clothing", 200),
            new("Slim-Fit Chino Trousers",
                "Stretch-cotton chinos with a tailored slim fit, two front slash pockets, and a zip fly.",
                54.99m, "Clothing", 150),
            new("Waterproof Hiking Jacket",
                "3-layer Gore-Tex shell with sealed seams, adjustable hood, and underarm venting zips.",
                189.99m, "Clothing", 60),
            new("Merino Wool Crew Jumper",
                "Fine-knit 100% merino wool jumper, rib-trim cuffs and hem, machine washable.",
                79.99m, "Clothing", 90),

            // Books
            new("Clean Code: A Handbook of Agile Software Craftsmanship",
                "Robert C. Martin's classic guide to writing readable, maintainable, and well-structured code.",
                34.99m, "Books", 75),
            new("Designing Data-Intensive Applications",
                "Martin Kleppmann's deep-dive into distributed systems, databases, and data engineering patterns.",
                44.99m, "Books", 50),
            new("The Pragmatic Programmer (20th Anniversary Edition)",
                "Updated anniversary edition of the seminal guide to software craftsmanship and developer career growth.",
                39.99m, "Books", 65),

            // Home & Kitchen
            new("Stainless Steel French Press",
                "34 oz double-wall insulated French press with a fine mesh filter and stay-cool handle.",
                34.99m, "Home & Kitchen", 110),
            new("Bamboo Cutting Board Set",
                "Set of three eco-friendly bamboo boards (small, medium, large) with juice grooves and non-slip feet.",
                29.99m, "Home & Kitchen", 95),
            new("Ceramic Pour-Over Coffee Dripper",
                "Single-serve ceramic dripper with a spiral ribbed interior for even extraction, fits standard 8–12 oz cups.",
                19.99m, "Home & Kitchen", 140),
        };

        _db.Products.AddRange(products);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Seeded {Count} products.", products.Count);
    }
}
