namespace ECom.Cart.UnitTests;

using ECom.Shared.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

[TestClass]
public sealed class ServiceBusConnectionStringValidationTests
{
    private static void AddBus(string? connectionString)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ServiceBus:ConnectionString"] = connectionString,
        }).Build();
        new ServiceCollection().AddEComServiceBus(configuration);
    }

    [TestMethod]
    public void NamespaceUrlInsteadOfConnectionString_ThrowsActionableError()
    {
        var ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            AddBus("https://ecommmerce.servicebus.windows.net/"));

        StringAssert.Contains(ex.Message, "ServiceBus:ConnectionString");
        StringAssert.Contains(ex.Message, "Shared access policies");
    }

    [TestMethod]
    public void EndpointWithoutKey_Throws()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            AddBus("Endpoint=sb://ecommmerce.servicebus.windows.net/"));
    }

    [TestMethod]
    public void ValidSasConnectionString_RegistersRealBus()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ServiceBus:ConnectionString"] =
                "Endpoint=sb://ecommmerce.servicebus.windows.net/;SharedAccessKeyName=consumers-listen;SharedAccessKey=ZmFrZS1rZXktZm9yLXRlc3Rz",
        }).Build();
        var services = new ServiceCollection();

        services.AddEComServiceBus(configuration);

        Assert.IsTrue(services.Any(d => d.ServiceType == typeof(IMessageBus)
                                         && d.ImplementationType == typeof(ServiceBusMessageBus)));
    }

    [TestMethod]
    public void EmptyConnectionString_RegistersUnconfiguredBusWithoutThrowing()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ServiceBus:ConnectionString"] = "",
        }).Build();
        var services = new ServiceCollection();

        services.AddEComServiceBus(configuration);

        var registration = services.Single(d => d.ServiceType == typeof(IMessageBus));
        Assert.AreNotEqual(typeof(ServiceBusMessageBus), registration.ImplementationType);
    }
}
