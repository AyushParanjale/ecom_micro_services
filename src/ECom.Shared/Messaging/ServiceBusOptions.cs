namespace ECom.Shared.Messaging;

using System.Text.Json;

public sealed record ServiceBusOptions
{
    public const string SectionName = "ServiceBus";

    /// <summary>Connection string for the Azure Service Bus namespace. Keep it in user-secrets / Key Vault.</summary>
    public string? ConnectionString { get; init; }

    /// <summary>
    /// Consumers only: false (default) receives from a queue, which works on the Basic tier.
    /// True receives from a topic subscription (Standard tier or above). Publishers work with either.
    /// </summary>
    public bool UseTopics { get; init; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString);
}

public static class TopicAndQueueNames
{
    public const string SectionName = "TopicAndQueueNames";
}

public static class EComJson
{
    /// <summary>Serializer settings used for every integration-event payload (camelCase, case-insensitive read).</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
