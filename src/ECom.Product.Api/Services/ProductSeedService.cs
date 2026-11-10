namespace ECom.Product.Api.Services;

using ECom.Product.Api.Data;
using ECom.Product.Api.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Idempotent catalogue seed: inserts any seed product whose name is not in the database yet and
/// backfills <see cref="Product.ImageUrl"/> on existing seed products that have none. Products created
/// or edited by an admin are never overwritten.
/// Images come from DummyJSON (cdn.dummyjson.com), Open Library covers, and Wikimedia Commons.
/// </summary>
public sealed class ProductSeedService
{
    private readonly ProductDbContext _db;
    private readonly ILogger<ProductSeedService> _logger;

    public ProductSeedService(ProductDbContext db, ILogger<ProductSeedService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedProductsAsync(CancellationToken cancellationToken = default)
    {
        var existing = await _db.Products.ToListAsync(cancellationToken);
        var existingByName = existing
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var added = 0;
        var imagesBackfilled = 0;

        foreach (var seed in SeedCatalogue())
        {
            if (!existingByName.TryGetValue(seed.Name, out var current))
            {
                _db.Products.Add(seed);
                existingByName[seed.Name] = seed;
                added++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(current.ImageUrl) && seed.ImageUrl is not null)
            {
                current.UpdateDetails(current.Name, current.Description, current.Price, current.Category, seed.ImageUrl);
                imagesBackfilled++;
            }
        }

        if (added == 0 && imagesBackfilled == 0)
        {
            _logger.LogInformation("Product seed skipped: catalogue is already up to date.");
            return;
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Product seed added {Added} products and backfilled {Backfilled} image URLs.",
            added, imagesBackfilled);
    }

    public static IReadOnlyList<Product> SeedCatalogue() =>
    [
        // Electronics
        new("Wireless Noise-Cancelling Headphones",
            "Over-ear headphones with active noise cancellation, 30-hour battery life, and foldable design.",
            149.99m, "Electronics", 42,
            "https://thumb.wikimedia.org/wikipedia/commons/thumb/0/0a/Bose_QuietComfort_25_Acoustic_Noise_Cancelling_Headphones_with_Carry_Case.jpg/960px-Bose_QuietComfort_25_Acoustic_Noise_Cancelling_Headphones_with_Carry_Case.jpg"),
        new("Mechanical Keyboard",
            "Tenkeyless mechanical keyboard with Cherry MX Brown switches, RGB backlighting, and USB-C cable.",
            89.99m, "Electronics", 30,
            "https://thumb.wikimedia.org/wikipedia/commons/thumb/5/5a/Mechanical_Keyboard.jpg/960px-Mechanical_Keyboard.jpg"),
        new("4K Webcam",
            "Ultra-HD webcam with autofocus, built-in ring light, and wide-angle 90° lens for streaming and video calls.",
            74.99m, "Electronics", 55,
            "https://thumb.wikimedia.org/wikipedia/commons/thumb/6/63/USB_webcam_for_PC.jpg/960px-USB_webcam_for_PC.jpg"),
        new("Portable Bluetooth Speaker",
            "IP67 waterproof speaker, 360° surround sound, 24-hour battery, and USB-C charging.",
            59.99m, "Electronics", 80,
            "https://cdn.dummyjson.com/product-images/mobile-accessories/apple-homepod-mini-cosmic-grey/thumbnail.webp"),
        new("USB-C Hub 7-in-1",
            "Multiport adapter with 4K HDMI, 2× USB-A, SD card reader, microSD, and 100W PD pass-through.",
            39.99m, "Electronics", 120,
            "https://thumb.wikimedia.org/wikipedia/commons/thumb/7/76/USB-C_Hubb_5_portar.jpg/960px-USB-C_Hubb_5_portar.jpg"),

        // Clothing
        new("Classic Crew-Neck T-Shirt",
            "100% organic cotton heavyweight tee, pre-shrunk, available in a range of neutral colors.",
            24.99m, "Clothing", 200,
            "https://cdn.dummyjson.com/product-images/mens-shirts/gigabyte-aorus-men-tshirt/thumbnail.webp"),
        new("Slim-Fit Chino Trousers",
            "Stretch-cotton chinos with a tailored slim fit, two front slash pockets, and a zip fly.",
            54.99m, "Clothing", 150,
            "https://thumb.wikimedia.org/wikipedia/commons/thumb/3/3e/Chino_pants.jpg/960px-Chino_pants.jpg"),
        new("Waterproof Hiking Jacket",
            "3-layer Gore-Tex shell with sealed seams, adjustable hood, and underarm venting zips.",
            189.99m, "Clothing", 60,
            "https://upload.wikimedia.org/wikipedia/commons/2/29/Yellow_Raincoat.jpg"),
        new("Merino Wool Crew Jumper",
            "Fine-knit 100% merino wool jumper, rib-trim cuffs and hem, machine washable.",
            79.99m, "Clothing", 90,
            "https://upload.wikimedia.org/wikipedia/commons/9/99/Navy_Wooly-Pully.JPG"),

        // Books
        new("Clean Code: A Handbook of Agile Software Craftsmanship",
            "Robert C. Martin's classic guide to writing readable, maintainable, and well-structured code.",
            34.99m, "Books", 75,
            "https://covers.openlibrary.org/b/isbn/9780132350884-L.jpg"),
        new("Designing Data-Intensive Applications",
            "Martin Kleppmann's deep-dive into distributed systems, databases, and data engineering patterns.",
            44.99m, "Books", 50,
            "https://covers.openlibrary.org/b/isbn/9781449373320-L.jpg"),
        new("The Pragmatic Programmer (20th Anniversary Edition)",
            "Updated anniversary edition of the seminal guide to software craftsmanship and developer career growth.",
            39.99m, "Books", 65,
            "https://covers.openlibrary.org/b/isbn/9780135957059-L.jpg"),

        // Home & Kitchen
        new("Stainless Steel French Press",
            "34 oz double-wall insulated French press with a fine mesh filter and stay-cool handle.",
            34.99m, "Home & Kitchen", 110,
            "https://thumb.wikimedia.org/wikipedia/commons/thumb/4/44/Stainless_steel_french_press.png/960px-Stainless_steel_french_press.png"),
        new("Bamboo Cutting Board Set",
            "Set of three eco-friendly bamboo boards (small, medium, large) with juice grooves and non-slip feet.",
            29.99m, "Home & Kitchen", 95,
            "https://cdn.dummyjson.com/product-images/kitchen-accessories/chopping-board/thumbnail.webp"),
        new("Ceramic Pour-Over Coffee Dripper",
            "Single-serve ceramic dripper with a spiral ribbed interior for even extraction, fits standard 8–12 oz cups.",
            19.99m, "Home & Kitchen", 140,
            "https://thumb.wikimedia.org/wikipedia/commons/thumb/d/df/Manual_drip_%28pour-over%29_coffee.jpg/960px-Manual_drip_%28pour-over%29_coffee.jpg"),

        // Electronics
        new("Apple MacBook Pro 14 Inch Space Grey",
            "The MacBook Pro 14 Inch in Space Grey is a powerful and sleek laptop, featuring Apple's M1 Pro chip for exceptional performance and a stunning Retina display.",
            1999.99m, "Electronics", 24,
            "https://cdn.dummyjson.com/product-images/laptops/apple-macbook-pro-14-inch-space-grey/thumbnail.webp"),
        new("New DELL XPS 13 9300 Laptop",
            "The New DELL XPS 13 9300 Laptop is a compact and powerful device, featuring a virtually borderless InfinityEdge display and high-end performance for various tasks.",
            1499.99m, "Electronics", 74,
            "https://cdn.dummyjson.com/product-images/laptops/new-dell-xps-13-9300-laptop/thumbnail.webp"),
        new("Huawei Matebook X Pro",
            "The Huawei Matebook X Pro is a slim and stylish laptop with a high-resolution touchscreen display, offering a premium experience for users on the go.",
            1399.99m, "Electronics", 75,
            "https://cdn.dummyjson.com/product-images/laptops/huawei-matebook-x-pro/thumbnail.webp"),
        new("iPhone 13 Pro",
            "The iPhone 13 Pro is a cutting-edge smartphone with a powerful camera system, high-performance chip, and stunning display. It offers advanced features for users who demand top-notch technology.",
            1099.99m, "Electronics", 56,
            "https://cdn.dummyjson.com/product-images/smartphones/iphone-13-pro/thumbnail.webp"),
        new("Samsung Galaxy S10",
            "The Samsung Galaxy S10 is a flagship device featuring a dynamic AMOLED display, versatile camera system, and powerful performance. It represents innovation and excellence in smartphone technology.",
            699.99m, "Electronics", 19,
            "https://cdn.dummyjson.com/product-images/smartphones/samsung-galaxy-s10/thumbnail.webp"),
        new("Oppo F19 Pro Plus",
            "The Oppo F19 Pro Plus is a feature-rich smartphone with a focus on camera capabilities. It boasts advanced photography features and a powerful performance for a premium user experience.",
            399.99m, "Electronics", 78,
            "https://cdn.dummyjson.com/product-images/smartphones/oppo-f19-pro-plus/thumbnail.webp"),
        new("iPad Mini 2021 Starlight",
            "The iPad Mini 2021 in Starlight is a compact and powerful tablet from Apple. Featuring a stunning Retina display, powerful A-series chip, and a sleek design, it offers a premium tablet experience.",
            499.99m, "Electronics", 47,
            "https://cdn.dummyjson.com/product-images/tablets/ipad-mini-2021-starlight/thumbnail.webp"),
        new("Samsung Galaxy Tab S8 Plus Grey",
            "The Samsung Galaxy Tab S8 Plus in Grey is a high-performance Android tablet by Samsung. With a large AMOLED display, powerful processor, and S Pen support, it's ideal for productivity and entertainment.",
            599.99m, "Electronics", 62,
            "https://cdn.dummyjson.com/product-images/tablets/samsung-galaxy-tab-s8-plus-grey/thumbnail.webp"),
        new("Apple Airpods",
            "The Apple Airpods offer a seamless wireless audio experience. With easy pairing, high-quality sound, and Siri integration, they are perfect for on-the-go listening.",
            129.99m, "Electronics", 67,
            "https://cdn.dummyjson.com/product-images/mobile-accessories/apple-airpods/thumbnail.webp"),
        new("Beats Flex Wireless Earphones",
            "The Beats Flex Wireless Earphones offer a comfortable and versatile audio experience. With magnetic earbuds and up to 12 hours of battery life, they are ideal for everyday use.",
            49.99m, "Electronics", 50,
            "https://cdn.dummyjson.com/product-images/mobile-accessories/beats-flex-wireless-earphones/thumbnail.webp"),
        new("Amazon Echo Plus",
            "The Amazon Echo Plus is a smart speaker with built-in Alexa voice control. It features premium sound quality and serves as a hub for controlling smart home devices.",
            99.99m, "Electronics", 61,
            "https://cdn.dummyjson.com/product-images/mobile-accessories/amazon-echo-plus/thumbnail.webp"),
        new("Apple Watch Series 4 Gold",
            "The Apple Watch Series 4 in Gold is a stylish and advanced smartwatch with features like heart rate monitoring, fitness tracking, and a beautiful Retina display.",
            349.99m, "Electronics", 33,
            "https://cdn.dummyjson.com/product-images/mobile-accessories/apple-watch-series-4-gold/thumbnail.webp"),

        // Clothing
        new("Blue & Black Check Shirt",
            "The Blue & Black Check Shirt is a stylish and comfortable men's shirt featuring a classic check pattern. Made from high-quality fabric, it's suitable for both casual and semi-formal occasions.",
            29.99m, "Clothing", 38,
            "https://cdn.dummyjson.com/product-images/mens-shirts/blue-&-black-check-shirt/thumbnail.webp"),
        new("Man Plaid Shirt",
            "The Man Plaid Shirt is a timeless and versatile men's shirt with a classic plaid pattern. Its comfortable fit and casual style make it a wardrobe essential for various occasions.",
            34.99m, "Clothing", 82,
            "https://cdn.dummyjson.com/product-images/mens-shirts/man-plaid-shirt/thumbnail.webp"),
        new("Blue Frock",
            "The Blue Frock is a charming and stylish dress for various occasions. With a vibrant blue color and a comfortable design, it adds a touch of elegance to your wardrobe.",
            29.99m, "Clothing", 52,
            "https://cdn.dummyjson.com/product-images/tops/blue-frock/thumbnail.webp"),
        new("Gray Dress",
            "The Gray Dress is a versatile and chic option for various occasions. With a neutral gray color, it can be dressed up or down, making it a wardrobe staple for any fashion-forward individual.",
            34.99m, "Clothing", 55,
            "https://cdn.dummyjson.com/product-images/tops/gray-dress/thumbnail.webp"),
        new("Tartan Dress",
            "The Tartan Dress features a classic tartan pattern, bringing a timeless and sophisticated touch to your wardrobe. Perfect for fall and winter, it adds a hint of traditional charm.",
            39.99m, "Clothing", 73,
            "https://cdn.dummyjson.com/product-images/tops/tartan-dress/thumbnail.webp"),
        new("Black Women's Gown",
            "The Black Women's Gown is an elegant and timeless evening gown. With a sleek black design, it's perfect for formal events and special occasions, exuding sophistication and style.",
            129.99m, "Clothing", 25,
            "https://cdn.dummyjson.com/product-images/womens-dresses/black-women's-gown/thumbnail.webp"),
        new("Marni Red & Black Suit",
            "The Marni Red & Black Suit is a sophisticated and fashion-forward suit ensemble. With a combination of red and black tones, it showcases a modern design for a bold and confident look.",
            179.99m, "Clothing", 62,
            "https://cdn.dummyjson.com/product-images/womens-dresses/marni-red-&-black-suit/thumbnail.webp"),

        // Footwear
        new("Nike Air Jordan 1 Red And Black",
            "The Nike Air Jordan 1 in Red and Black is an iconic basketball sneaker known for its stylish design and high-performance features, making it a favorite among sneaker enthusiasts and athletes.",
            149.99m, "Footwear", 25,
            "https://cdn.dummyjson.com/product-images/mens-shoes/nike-air-jordan-1-red-and-black/thumbnail.webp"),
        new("Puma Future Rider Trainers",
            "The Puma Future Rider Trainers offer a blend of retro style and modern comfort. Perfect for casual wear, these trainers provide a fashionable and comfortable option for everyday use.",
            89.99m, "Footwear", 90,
            "https://cdn.dummyjson.com/product-images/mens-shoes/puma-future-rider-trainers/thumbnail.webp"),
        new("Sports Sneakers Off White & Red",
            "The Sports Sneakers in Off White and Red combine style and functionality, making them a fashionable choice for sports enthusiasts. The red and off-white color combination adds a bold and energetic touch.",
            119.99m, "Footwear", 17,
            "https://cdn.dummyjson.com/product-images/mens-shoes/sports-sneakers-off-white-&-red/thumbnail.webp"),
        new("Calvin Klein Heel Shoes",
            "Calvin Klein Heel Shoes are elegant and sophisticated, designed for formal occasions. With a classic design and high-quality materials, they complement your stylish ensemble.",
            79.99m, "Footwear", 93,
            "https://cdn.dummyjson.com/product-images/womens-shoes/calvin-klein-heel-shoes/thumbnail.webp"),
        new("Golden Shoes Woman",
            "The Golden Shoes for Women are a glamorous choice for special occasions. Featuring a golden hue and stylish design, they add a touch of luxury to your outfit.",
            49.99m, "Footwear", 88,
            "https://cdn.dummyjson.com/product-images/womens-shoes/golden-shoes-woman/thumbnail.webp"),

        // Accessories
        new("Brown Leather Belt Watch",
            "The Brown Leather Belt Watch is a stylish timepiece with a classic design. Featuring a genuine leather strap and a sleek dial, it adds a touch of sophistication to your look.",
            89.99m, "Accessories", 32,
            "https://cdn.dummyjson.com/product-images/mens-watches/brown-leather-belt-watch/thumbnail.webp"),
        new("Longines Master Collection",
            "The Longines Master Collection is an elegant and refined watch known for its precision and craftsmanship. With a timeless design, it's a symbol of luxury and sophistication.",
            1499.99m, "Accessories", 100,
            "https://cdn.dummyjson.com/product-images/mens-watches/longines-master-collection/thumbnail.webp"),
        new("Black Sun Glasses",
            "The Black Sun Glasses are a classic and stylish choice, featuring a sleek black frame and tinted lenses. They provide both UV protection and a fashionable look.",
            29.99m, "Accessories", 60,
            "https://cdn.dummyjson.com/product-images/sunglasses/black-sun-glasses/thumbnail.webp"),
        new("Green and Black Glasses",
            "The Green and Black Glasses feature a bold combination of green and black colors, adding a touch of vibrancy to your eyewear collection. They are both stylish and eye-catching.",
            34.99m, "Accessories", 24,
            "https://cdn.dummyjson.com/product-images/sunglasses/green-and-black-glasses/thumbnail.webp"),
        new("Blue Women's Handbag",
            "The Blue Women's Handbag is a stylish and spacious accessory for everyday use. With a vibrant blue color and multiple compartments, it combines fashion and functionality.",
            49.99m, "Accessories", 76,
            "https://cdn.dummyjson.com/product-images/womens-bags/blue-women's-handbag/thumbnail.webp"),
        new("Heshe Women's Leather Bag",
            "The Heshe Women's Leather Bag is a luxurious and high-quality leather bag for the sophisticated woman. With a timeless design and durable craftsmanship, it's a versatile accessory.",
            129.99m, "Accessories", 99,
            "https://cdn.dummyjson.com/product-images/womens-bags/heshe-women's-leather-bag/thumbnail.webp"),
        new("White Faux Leather Backpack",
            "The White Faux Leather Backpack is a trendy and practical backpack for the modern woman. With a sleek white design and ample storage space, it's perfect for both casual and on-the-go styles.",
            39.99m, "Accessories", 39,
            "https://cdn.dummyjson.com/product-images/womens-bags/white-faux-leather-backpack/thumbnail.webp"),
        new("Green Crystal Earring",
            "The Green Crystal Earring is a dazzling accessory that features a vibrant green crystal. With a classic design, it adds a touch of elegance to your ensemble, perfect for formal or special occasions.",
            29.99m, "Accessories", 54,
            "https://cdn.dummyjson.com/product-images/womens-jewellery/green-crystal-earring/thumbnail.webp"),

        // Beauty
        new("Essence Mascara Lash Princess",
            "The Essence Mascara Lash Princess is a popular mascara known for its volumizing and lengthening effects. Achieve dramatic lashes with this long-lasting and cruelty-free formula.",
            9.99m, "Beauty", 99,
            "https://cdn.dummyjson.com/product-images/beauty/essence-mascara-lash-princess/thumbnail.webp"),
        new("Eyeshadow Palette with Mirror",
            "The Eyeshadow Palette with Mirror offers a versatile range of eyeshadow shades for creating stunning eye looks. With a built-in mirror, it's convenient for on-the-go makeup application.",
            19.99m, "Beauty", 34,
            "https://cdn.dummyjson.com/product-images/beauty/eyeshadow-palette-with-mirror/thumbnail.webp"),
        new("Red Lipstick",
            "The Red Lipstick is a classic and bold choice for adding a pop of color to your lips. With a creamy and pigmented formula, it provides a vibrant and long-lasting finish.",
            12.99m, "Beauty", 91,
            "https://cdn.dummyjson.com/product-images/beauty/red-lipstick/thumbnail.webp"),
        new("Chanel Coco Noir Eau De",
            "Coco Noir by Chanel is an elegant and mysterious fragrance, featuring notes of grapefruit, rose, and sandalwood. Perfect for evening occasions.",
            129.99m, "Beauty", 58,
            "https://cdn.dummyjson.com/product-images/fragrances/chanel-coco-noir-eau-de/thumbnail.webp"),
        new("Dior J'adore",
            "J'adore by Dior is a luxurious and floral fragrance, known for its blend of ylang-ylang, rose, and jasmine. It embodies femininity and sophistication.",
            89.99m, "Beauty", 98,
            "https://cdn.dummyjson.com/product-images/fragrances/dior-j'adore/thumbnail.webp"),
        new("Gucci Bloom Eau de",
            "Gucci Bloom by Gucci is a floral and captivating fragrance, with notes of tuberose, jasmine, and Rangoon creeper. It's a modern and romantic scent.",
            79.99m, "Beauty", 91,
            "https://cdn.dummyjson.com/product-images/fragrances/gucci-bloom-eau-de/thumbnail.webp"),
        new("Olay Ultra Moisture Shea Butter Body Wash",
            "Olay Ultra Moisture Shea Butter Body Wash is a luxurious body wash that hydrates and nourishes your skin with the moisturizing power of shea butter. Enjoy a rich lather and silky-smooth skin.",
            12.99m, "Beauty", 34,
            "https://cdn.dummyjson.com/product-images/skin-care/olay-ultra-moisture-shea-butter-body-wash/thumbnail.webp"),

        // Home & Kitchen
        new("Table Lamp",
            "The Table Lamp is a functional and decorative lighting solution for your living space. With a modern design, it provides both ambient and task lighting, enhancing the atmosphere.",
            49.99m, "Home & Kitchen", 25,
            "https://cdn.dummyjson.com/product-images/home-decoration/table-lamp/thumbnail.webp"),
        new("Plant Pot",
            "The Plant Pot is a stylish container for your favorite plants. With a sleek design, it complements your indoor or outdoor garden, adding a modern touch to your plant display.",
            14.99m, "Home & Kitchen", 59,
            "https://cdn.dummyjson.com/product-images/home-decoration/plant-pot/thumbnail.webp"),
        new("Family Tree Photo Frame",
            "The Family Tree Photo Frame is a sentimental and stylish way to display your cherished family memories. With multiple photo slots, it tells the story of your loved ones.",
            29.99m, "Home & Kitchen", 77,
            "https://cdn.dummyjson.com/product-images/home-decoration/family-tree-photo-frame/thumbnail.webp"),
        new("Boxed Blender",
            "The Boxed Blender is a powerful and compact blender perfect for smoothies, shakes, and more. Its convenient design and multiple functions make it a versatile kitchen appliance.",
            39.99m, "Home & Kitchen", 25,
            "https://cdn.dummyjson.com/product-images/kitchen-accessories/boxed-blender/thumbnail.webp"),
        new("Carbon Steel Wok",
            "The Carbon Steel Wok is a versatile cooking pan suitable for stir-frying, sautéing, and deep frying. Its sturdy construction ensures even heat distribution for delicious meals.",
            29.99m, "Home & Kitchen", 40,
            "https://cdn.dummyjson.com/product-images/kitchen-accessories/carbon-steel-wok/thumbnail.webp"),
        new("Hand Blender",
            "The Hand Blender is a versatile kitchen appliance for blending, pureeing, and mixing. Its compact design and powerful motor make it a convenient tool for various recipes.",
            34.99m, "Home & Kitchen", 84,
            "https://cdn.dummyjson.com/product-images/kitchen-accessories/hand-blender/thumbnail.webp"),
        new("Microwave Oven",
            "The Microwave Oven is a versatile kitchen appliance for quick and efficient cooking, reheating, and defrosting. Its compact size makes it suitable for various kitchen setups.",
            89.99m, "Home & Kitchen", 59,
            "https://cdn.dummyjson.com/product-images/kitchen-accessories/microwave-oven/thumbnail.webp"),
        new("Pan",
            "The Pan is a versatile and essential cookware item for frying, sautéing, and cooking various dishes. Its non-stick coating ensures easy food release and cleanup.",
            24.99m, "Home & Kitchen", 90,
            "https://cdn.dummyjson.com/product-images/kitchen-accessories/pan/thumbnail.webp"),
        new("Silver Pot With Glass Cap",
            "The Silver Pot with Glass Cap is a stylish and functional cookware item for boiling, simmering, and preparing delicious meals. Its glass cap allows you to monitor cooking progress.",
            39.99m, "Home & Kitchen", 40,
            "https://cdn.dummyjson.com/product-images/kitchen-accessories/silver-pot-with-glass-cap/thumbnail.webp"),
        new("Spice Rack",
            "The Spice Rack is a convenient organizer for your spices and seasonings. Keep your kitchen essentials within reach and neatly arranged with this stylish spice rack.",
            19.99m, "Home & Kitchen", 79,
            "https://cdn.dummyjson.com/product-images/kitchen-accessories/spice-rack/thumbnail.webp"),

        // Furniture
        new("Bedside Table African Cherry",
            "The Bedside Table in African Cherry is a stylish and functional addition to your bedroom, providing convenient storage space and a touch of elegance.",
            299.99m, "Furniture", 64,
            "https://cdn.dummyjson.com/product-images/furniture/bedside-table-african-cherry/thumbnail.webp"),
        new("Knoll Saarinen Executive Conference Chair",
            "The Knoll Saarinen Executive Conference Chair is a modern and ergonomic chair, perfect for your office or conference room with its timeless design.",
            499.99m, "Furniture", 26,
            "https://cdn.dummyjson.com/product-images/furniture/knoll-saarinen-executive-conference-chair/thumbnail.webp"),

        // Sports & Outdoors
        new("Basketball",
            "The Basketball is a standard ball used in basketball games. It is designed for dribbling, shooting, and passing in the game of basketball, suitable for both indoor and outdoor play.",
            14.99m, "Sports & Outdoors", 75,
            "https://cdn.dummyjson.com/product-images/sports-accessories/basketball/thumbnail.webp"),
        new("Cricket Bat",
            "The Cricket Bat is an essential piece of cricket equipment used by batsmen to hit the cricket ball. It is made of wood and comes in various sizes and designs.",
            29.99m, "Sports & Outdoors", 98,
            "https://cdn.dummyjson.com/product-images/sports-accessories/cricket-bat/thumbnail.webp"),
        new("Football",
            "The Football, also known as a soccer ball, is the standard ball used in the sport of football (soccer). It is designed for kicking and passing in the game.",
            17.99m, "Sports & Outdoors", 96,
            "https://cdn.dummyjson.com/product-images/sports-accessories/football/thumbnail.webp"),
        new("Iron Golf",
            "The Iron Golf is a type of golf club designed for various golf shots. It features a solid metal head and is used for approach shots, chipping, and other golfing techniques.",
            49.99m, "Sports & Outdoors", 90,
            "https://cdn.dummyjson.com/product-images/sports-accessories/iron-golf/thumbnail.webp"),
        new("Tennis Racket",
            "The Tennis Racket is an essential piece of equipment used in the sport of tennis. It features a frame with strings and a grip, allowing players to hit the tennis ball.",
            49.99m, "Sports & Outdoors", 25,
            "https://cdn.dummyjson.com/product-images/sports-accessories/tennis-racket/thumbnail.webp"),
        new("Tennis Ball",
            "The Tennis Ball is a standard ball used in the sport of tennis. It is designed for bouncing and hitting with tennis rackets during matches or practice sessions.",
            6.99m, "Sports & Outdoors", 28,
            "https://cdn.dummyjson.com/product-images/sports-accessories/tennis-ball/thumbnail.webp"),

        // Groceries
        new("Honey Jar",
            "Pure and natural honey in a convenient jar, perfect for sweetening beverages or drizzling over food.",
            6.99m, "Groceries", 34,
            "https://cdn.dummyjson.com/product-images/groceries/honey-jar/thumbnail.webp"),
        new("Nescafe Coffee",
            "Quality coffee from Nescafe, available in various blends for a rich and satisfying cup.",
            7.99m, "Groceries", 57,
            "https://cdn.dummyjson.com/product-images/groceries/nescafe-coffee/thumbnail.webp"),
        new("Protein Powder",
            "Nutrient-packed protein powder, ideal for supplementing your diet with essential proteins.",
            19.99m, "Groceries", 80,
            "https://cdn.dummyjson.com/product-images/groceries/protein-powder/thumbnail.webp")
    ];
}
