namespace ECom.Shared.Messaging;

using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class ServiceBusServiceCollectionExtensions
{
    /// <summary>
    /// Registers a singleton <see cref="ServiceBusClient"/> and <see cref="IMessageBus"/> when
    /// <c>ServiceBus:ConnectionString</c> is configured; otherwise a publisher that always fails
    /// (so outbox rows are retried later) and consumers that stay disabled.
    /// </summary>
    public static IServiceCollection AddEComServiceBus(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(ServiceBusOptions.SectionName);
        services.Configure<ServiceBusOptions>(section);
        var options = section.Get<ServiceBusOptions>() ?? new ServiceBusOptions();

        if (options.IsConfigured)
        {
            EnsureValidConnectionString(options.ConnectionString!);
            services.AddSingleton(_ => new ServiceBusClient(options.ConnectionString));
            services.AddSingleton<IMessageBus, ServiceBusMessageBus>();
        }
        else
        {
            services.AddSingleton<IMessageBus, UnconfiguredMessageBus>();
        }

        return services;
    }

    /// <summary>
    /// Fails at startup with an actionable message instead of crashing later inside a consumer,
    /// e.g. when the namespace host name was pasted instead of a SAS connection string.
    /// </summary>
    internal static void EnsureValidConnectionString(string connectionString)
    {
        const string expected =
            "Expected a SAS connection string such as 'Endpoint=sb://<namespace>.servicebus.windows.net/;" +
            "SharedAccessKeyName=<policy>;SharedAccessKey=<key>' (Azure portal: Service Bus namespace → " +
            "Shared access policies → <policy> → Primary Connection String).";

        ServiceBusConnectionStringProperties properties;
        try
        {
            properties = ServiceBusConnectionStringProperties.Parse(connectionString);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            throw new InvalidOperationException(
                $"'{ServiceBusOptions.SectionName}:ConnectionString' is not a valid Service Bus connection string. {expected}");
        }

        var hasCredential =
            (!string.IsNullOrEmpty(properties.SharedAccessKeyName) && !string.IsNullOrEmpty(properties.SharedAccessKey)) ||
            !string.IsNullOrEmpty(properties.SharedAccessSignature);

        if (properties.Endpoint is null || !hasCredential)
            throw new InvalidOperationException(
                $"'{ServiceBusOptions.SectionName}:ConnectionString' is missing the endpoint or the shared access key. {expected}");
    }

    /// <summary>
    /// Registers <typeparamref name="THandler"/> and a background consumer. The entity name comes from
    /// <c>TopicAndQueueNames:{entityKey}</c>. With <c>ServiceBus:UseTopics=false</c> (default) it is read
    /// as a queue; with <c>true</c> it is a topic and <c>TopicAndQueueNames:{subscriptionKey}</c> names
    /// the subscription.
    /// </summary>
    public static IServiceCollection AddServiceBusConsumer<TMessage, THandler>(
        this IServiceCollection services,
        IConfiguration configuration,
        string entityKey,
        string subscriptionKey)
        where THandler : class, IIntegrationEventHandler<TMessage>
    {
        var options = configuration.GetSection(ServiceBusOptions.SectionName).Get<ServiceBusOptions>() ?? new ServiceBusOptions();
        var names = configuration.GetSection(TopicAndQueueNames.SectionName);

        var entityName = names[entityKey]
            ?? throw new InvalidOperationException($"'{TopicAndQueueNames.SectionName}:{entityKey}' is not configured.");

        string? subscription = null;
        if (options.UseTopics)
        {
            subscription = names[subscriptionKey]
                ?? throw new InvalidOperationException(
                    $"'{TopicAndQueueNames.SectionName}:{subscriptionKey}' is required when '{ServiceBusOptions.SectionName}:UseTopics' is true.");
        }

        services.AddSingleton(new ServiceBusSource<TMessage>(entityName, subscription));
        services.AddScoped<IIntegrationEventHandler<TMessage>, THandler>();
        services.AddHostedService<ServiceBusConsumer<TMessage>>();
        return services;
    }
}
