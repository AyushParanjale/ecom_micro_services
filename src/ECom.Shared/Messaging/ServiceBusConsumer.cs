namespace ECom.Shared.Messaging;

using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Where a consumer receives <typeparamref name="TMessage"/> from: a queue when
/// <see cref="Subscription"/> is null, otherwise a subscription on the topic <see cref="EntityName"/>.
/// </summary>
public sealed record ServiceBusSource<TMessage>(string EntityName, string? Subscription)
{
    public bool IsQueue => Subscription is null;

    public override string ToString() => IsQueue ? $"queue '{EntityName}'" : $"topic '{EntityName}' / subscription '{Subscription}'";
}

/// <summary>
/// Background consumer for one queue or topic subscription. Creates a DI scope per message, completes
/// the message only after the handler succeeds, and lets failures propagate so Service Bus abandons,
/// retries and finally dead-letters the message. Payloads that cannot be deserialized are
/// dead-lettered immediately.
/// </summary>
public sealed class ServiceBusConsumer<TMessage> : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ServiceBusSource<TMessage> _source;
    private readonly ILogger<ServiceBusConsumer<TMessage>> _logger;
    private ServiceBusProcessor? _processor;

    public ServiceBusConsumer(
        IServiceProvider serviceProvider,
        IServiceScopeFactory scopeFactory,
        ServiceBusSource<TMessage> source,
        ILogger<ServiceBusConsumer<TMessage>> logger)
    {
        _serviceProvider = serviceProvider;
        _scopeFactory = scopeFactory;
        _source = source;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var client = _serviceProvider.GetService<ServiceBusClient>();
        if (client is null)
        {
            _logger.LogWarning("Azure Service Bus is not configured; consumer for {Source} is disabled.", _source);
            return;
        }

        var options = new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = 4
        };

        _processor = _source.IsQueue
            ? client.CreateProcessor(_source.EntityName, options)
            : client.CreateProcessor(_source.EntityName, _source.Subscription, options);

        _processor.ProcessMessageAsync += HandleMessageAsync;
        _processor.ProcessErrorAsync += args =>
        {
            _logger.LogError(args.Exception,
                "Service Bus error on {Entity} ({Source})", args.EntityPath, args.ErrorSource);
            return Task.CompletedTask;
        };

        await _processor.StartProcessingAsync(stoppingToken);
        _logger.LogInformation("Listening on {Source}", _source);
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        TMessage? message;
        try
        {
            message = args.Message.Body.ToObjectFromJson<TMessage>(EComJson.Options);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Dead-lettering undeserializable message {MessageId} on {Source}",
                args.Message.MessageId, _source);
            await args.DeadLetterMessageAsync(args.Message, "DeserializationFailed", ex.Message, args.CancellationToken);
            return;
        }

        if (message is null)
        {
            await args.DeadLetterMessageAsync(args.Message, "EmptyPayload", "Message body deserialized to null.", args.CancellationToken);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<IIntegrationEventHandler<TMessage>>();
        await handler.HandleAsync(message, args.Message.MessageId, args.CancellationToken);
        await args.CompleteMessageAsync(args.Message, args.CancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor is not null)
        {
            await _processor.StopProcessingAsync(cancellationToken);
            await _processor.DisposeAsync();
        }
        await base.StopAsync(cancellationToken);
    }
}
