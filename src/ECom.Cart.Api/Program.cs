using ECom.Cart.Api.Clients;
using ECom.Cart.Api.Data;
using ECom.Cart.Api.Messaging;
using ECom.Cart.Api.Repositories;
using ECom.Cart.Api.Services;
using ECom.Shared.Auth;
using ECom.Shared.Hosting;
using ECom.Shared.Http;
using ECom.Shared.Messaging;
using Microsoft.EntityFrameworkCore;

await EComHostingExtensions.RunEComAsync(async () =>
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddEComSerilog();
    builder.Services.AddEComApiDefaults("ECom Cart API", "Shopping cart");

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
    builder.Services.AddDbContext<CartDbContext>(options => options.UseSqlServer(connectionString));

    builder.Services.AddEComJwtAuthentication(builder.Configuration);

    builder.Services.AddDownstreamClient<IProductCatalogClient, ProductCatalogClient>(builder.Configuration, "ProductApi");

    builder.Services.AddScoped<ICartRepository, CartRepository>();
    builder.Services.AddScoped<ICartService, CartService>();

    builder.Services.AddEComServiceBus(builder.Configuration);
    builder.Services.AddServiceBusConsumer<OrderCreatedEvent, OrderCreatedHandler>(
        builder.Configuration, "OrderCreated", "OrderCreatedCartSubscription");

    var app = builder.Build();

    app.UseEComApiDefaults("ECom Cart API");

    await app.RunAsync();
});

public partial class Program { }
