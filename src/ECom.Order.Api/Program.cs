using ECom.Order.Api.Clients;
using ECom.Order.Api.Data;
using ECom.Order.Api.Hubs;
using ECom.Order.Api.Messaging;
using ECom.Order.Api.Options;
using ECom.Order.Api.Repositories;
using ECom.Order.Api.Services;
using ECom.Shared.Auth;
using ECom.Shared.Hosting;
using ECom.Shared.Http;
using ECom.Shared.Messaging;
using ECom.Shared.Outbox;
using Microsoft.EntityFrameworkCore;

const string OrderHubPath = "/hubs/orders";

await EComHostingExtensions.RunEComAsync(async () =>
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddEComSerilog();
    builder.Services.AddEComApiDefaults("ECom Order API", "Checkout, orders and payments");

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
    builder.Services.AddDbContext<OrderDbContext>(options => options.UseSqlServer(connectionString));

    builder.Services.AddEComJwtAuthentication(builder.Configuration, signalRHubPath: OrderHubPath);
    builder.Services.AddSignalR();

    builder.Services.AddDownstreamClient<ICartClient, CartClient>(builder.Configuration, "CartApi");
    builder.Services.AddDownstreamClient<IProductClient, ProductClient>(builder.Configuration, "ProductApi");

    builder.Services.Configure<FakePaymentOptions>(builder.Configuration.GetSection(FakePaymentOptions.SectionName));

    builder.Services.AddScoped<IOrderRepository, OrderRepository>();
    builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
    builder.Services.AddScoped<IOrderService, OrderService>();
    builder.Services.AddScoped<IPaymentService, PaymentService>();
    builder.Services.AddScoped<IPaymentGateway, FakePaymentGateway>();
    builder.Services.AddScoped<IOrderNotifier, SignalROrderNotifier>();

    // Transactional outbox: SignalR notifications + integration events published to Service Bus
    // (each entity name may be a queue or a topic; the sender works with either).
    builder.Services.AddEComServiceBus(builder.Configuration);
    builder.Services.AddEComOutbox<OrderDbContext>(builder.Configuration);

    var entityNames = builder.Configuration.GetSection(TopicAndQueueNames.SectionName);
    var orderCreatedEntity = entityNames["OrderCreated"]
        ?? throw new InvalidOperationException("'TopicAndQueueNames:OrderCreated' is not configured.");
    var orderPaymentFailedEntity = entityNames["OrderPaymentFailed"]
        ?? throw new InvalidOperationException("'TopicAndQueueNames:OrderPaymentFailed' is not configured.");

    builder.Services.AddScoped<IOutboxMessageHandler, OrderStatusChangedOutboxHandler>();
    builder.Services.AddScoped<IOutboxMessageHandler>(sp => new IntegrationEventOutboxHandler(
        OutboxEventTypes.OrderCreated, orderCreatedEntity, sp.GetRequiredService<IMessageBus>()));
    builder.Services.AddScoped<IOutboxMessageHandler>(sp => new IntegrationEventOutboxHandler(
        OutboxEventTypes.OrderPaymentFailed, orderPaymentFailedEntity, sp.GetRequiredService<IMessageBus>()));

    var app = builder.Build();

    app.UseEComApiDefaults("ECom Order API");
    app.MapHub<OrderHub>(OrderHubPath);

    await app.RunAsync();
});

public partial class Program { }
