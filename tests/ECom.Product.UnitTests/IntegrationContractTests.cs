namespace ECom.Product.UnitTests;

using System.Text.Json;
using ECom.Product.Api.Messaging;
using ECom.Shared.Messaging;

[TestClass]
public sealed class IntegrationContractTests
{
    // Payload shape written to the Order service outbox (OrderPaymentFailed) and published to Service Bus.
    private const string OrderPaymentFailedPayload =
        "{\"orderId\":\"4cc6182f-82cf-400c-9eb1-733c6400e07e\",\"userId\":\"14f4d397-49d3-4dc9-03b1-08df34cca441\"," +
        "\"items\":[{\"productId\":\"afe9d195-1722-4c1c-b767-0a092f851b84\",\"quantity\":3}]," +
        "\"occurredAt\":\"2026-10-28T08:25:45.8611849+00:00\"}";

    [TestMethod]
    public void OrderPaymentFailedEvent_DeserializesFromOrderServicePayload()
    {
        var message = BinaryData.FromString(OrderPaymentFailedPayload).ToObjectFromJson<OrderPaymentFailedEvent>(EComJson.Options);

        Assert.IsNotNull(message);
        Assert.AreEqual(Guid.Parse("4cc6182f-82cf-400c-9eb1-733c6400e07e"), message.OrderId);
        Assert.AreEqual(Guid.Parse("14f4d397-49d3-4dc9-03b1-08df34cca441"), message.UserId);
        Assert.HasCount(1, message.Items);
        Assert.AreEqual(Guid.Parse("afe9d195-1722-4c1c-b767-0a092f851b84"), message.Items[0].ProductId);
        Assert.AreEqual(3, message.Items[0].Quantity);
    }

    [TestMethod]
    public void OrderPaymentFailedEvent_MalformedPayload_ThrowsJsonException()
    {
        Assert.ThrowsExactly<JsonException>(() =>
            BinaryData.FromString("{\"orderId\":42}").ToObjectFromJson<OrderPaymentFailedEvent>(EComJson.Options));
    }
}
