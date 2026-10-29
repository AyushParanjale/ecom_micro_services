using ECom.Product.Api.Data;
using ECom.Product.Api.Messaging;
using ECom.Product.Api.Options;
using ECom.Product.Api.Repositories;
using ECom.Product.Api.Services;
using ECom.Shared.Auth;
using ECom.Shared.Caching;
using ECom.Shared.Hosting;
using ECom.Shared.Messaging;
using Microsoft.EntityFrameworkCore;

await EComHostingExtensions.RunEComAsync(async () =>
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddEComSerilog();
    builder.Services.AddEComApiDefaults("ECom Product API", "Product catalog and stock");

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
    builder.Services.AddDbContext<ProductDbContext>(options => options.UseSqlServer(connectionString));

    builder.Services.AddEComJwtAuthentication(builder.Configuration);

    builder.Services.Configure<CachingOptions>(builder.Configuration.GetSection(CachingOptions.SectionName));
    builder.Services.AddEComMemoryCache();

    builder.Services.AddScoped<IProductRepository, ProductRepository>();
    builder.Services.AddScoped<IStockOperationRepository, StockOperationRepository>();
    builder.Services.AddScoped<IProductService, ProductService>();
    builder.Services.AddScoped<IStockService, StockService>();
    builder.Services.AddScoped<ProductSeedService>();

    builder.Services.AddEComServiceBus(builder.Configuration);
    builder.Services.AddServiceBusConsumer<OrderPaymentFailedEvent, OrderPaymentFailedHandler>(
        builder.Configuration, "OrderPaymentFailed", "OrderPaymentFailedProductSubscription");

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();
        try
        {
            await scope.ServiceProvider.GetRequiredService<ProductSeedService>().SeedProductsAsync();
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Product seed failed.");
        }
    }

    app.UseEComApiDefaults("ECom Product API");

    await app.RunAsync();
});

public partial class Program { }
