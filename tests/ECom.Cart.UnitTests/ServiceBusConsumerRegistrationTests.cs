namespace ECom.Cart.UnitTests;

using ECom.Cart.Api.Messaging;
using ECom.Shared.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

[TestClass]
public sealed class ServiceBusConsumerRegistrationTests
{
    private static ServiceProvider Register(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddServiceBusConsumer<OrderCreatedEvent, OrderCreatedHandler>(
            configuration, "OrderCreated", "OrderCreatedCartSubscription");
        return services.BuildServiceProvider();
    }

    [TestMethod]
    public void UseTopicsFalse_RegistersQueueSource()
    {
        using var provider = Register(new()
        {
            ["ServiceBus:UseTopics"] = "false",
            ["TopicAndQueueNames:OrderCreated"] = "ordercreated",
            ["TopicAndQueueNames:OrderCreatedCartSubscription"] = "OrderCreatedCart",
        });

        var source = provider.GetRequiredService<ServiceBusSource<OrderCreatedEvent>>();
        Assert.IsTrue(source.IsQueue);
        Assert.AreEqual("ordercreated", source.EntityName);
        Assert.IsNull(source.Subscription);
    }

    [TestMethod]
    public void UseTopicsMissing_DefaultsToQueue()
    {
        using var provider = Register(new()
        {
            ["TopicAndQueueNames:OrderCreated"] = "ordercreated",
        });

        Assert.IsTrue(provider.GetRequiredService<ServiceBusSource<OrderCreatedEvent>>().IsQueue);
    }

    [TestMethod]
    public void UseTopicsTrue_RegistersTopicSubscriptionSource()
    {
        using var provider = Register(new()
        {
            ["ServiceBus:UseTopics"] = "true",
            ["TopicAndQueueNames:OrderCreated"] = "ordercreated",
            ["TopicAndQueueNames:OrderCreatedCartSubscription"] = "OrderCreatedCart",
        });

        var source = provider.GetRequiredService<ServiceBusSource<OrderCreatedEvent>>();
        Assert.IsFalse(source.IsQueue);
        Assert.AreEqual("ordercreated", source.EntityName);
        Assert.AreEqual("OrderCreatedCart", source.Subscription);
    }

    [TestMethod]
    public void UseTopicsTrue_WithoutSubscriptionName_Throws()
    {
        var ex = Assert.ThrowsExactly<InvalidOperationException>(() => Register(new()
        {
            ["ServiceBus:UseTopics"] = "true",
            ["TopicAndQueueNames:OrderCreated"] = "ordercreated",
        }));
        StringAssert.Contains(ex.Message, "OrderCreatedCartSubscription");
    }

    [TestMethod]
    public void MissingEntityName_Throws()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => Register(new()));
    }

    [TestMethod]
    public void RegistersHandlerAndHostedConsumer()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TopicAndQueueNames:OrderCreated"] = "ordercreated",
        }).Build();
        var services = new ServiceCollection();

        services.AddServiceBusConsumer<OrderCreatedEvent, OrderCreatedHandler>(
            configuration, "OrderCreated", "OrderCreatedCartSubscription");

        Assert.IsTrue(services.Any(d => d.ServiceType == typeof(IIntegrationEventHandler<OrderCreatedEvent>)
                                         && d.ImplementationType == typeof(OrderCreatedHandler)));
        Assert.IsTrue(services.Any(d => d.ServiceType == typeof(IHostedService)
                                         && d.ImplementationType == typeof(ServiceBusConsumer<OrderCreatedEvent>)));
    }
}
