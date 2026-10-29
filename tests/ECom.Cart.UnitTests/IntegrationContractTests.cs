namespace ECom.Cart.UnitTests;

using ECom.Cart.Api.Messaging;
using ECom.Shared.Messaging;

[TestClass]
public sealed class IntegrationContractTests
{
    // Payload shape written to the Order service outbox (OrderCreated) and published to Service Bus.
    private const string OrderCreatedPayload =
        "{\"orderId\":\"a88a6b0f-6725-4114-bcd0-2a3ee34ad1b7\",\"userId\":\"14b0b8e7-9e59-4511-03b0-08df34cca441\"," +
        "\"items\":[{\"productId\":\"963a5839-aabf-4a5a-aa2d-0146adc12793\",\"quantity\":1}]," +
        "\"occurredAt\":\"2026-10-28T08:24:49.729606+00:00\"}";

    [TestMethod]
    public void OrderCreatedEvent_DeserializesFromOrderServicePayload()
    {
        var message = BinaryData.FromString(OrderCreatedPayload).ToObjectFromJson<OrderCreatedEvent>(EComJson.Options);

        Assert.IsNotNull(message);
        Assert.AreEqual(Guid.Parse("a88a6b0f-6725-4114-bcd0-2a3ee34ad1b7"), message.OrderId);
        Assert.AreEqual(Guid.Parse("14b0b8e7-9e59-4511-03b0-08df34cca441"), message.UserId);
        Assert.HasCount(1, message.Items);
        Assert.AreEqual(Guid.Parse("963a5839-aabf-4a5a-aa2d-0146adc12793"), message.Items[0].ProductId);
        Assert.AreEqual(1, message.Items[0].Quantity);
    }
}
