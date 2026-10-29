namespace ECom.Shared.Outbox;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// Single-instance assumption: this implementation does not use distributed locking.
// Multiple application instances would process outbox messages concurrently and could
// dispatch duplicates; consumers are idempotent and SignalR notifications are advisory.
public sealed class OutboxDispatchWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<OutboxOptions> _options;
    private readonly ILogger<OutboxDispatchWorker> _logger;

    public OutboxDispatchWorker(
        IServiceProvider serviceProvider,
        IOptions<OutboxOptions> options,
        ILogger<OutboxDispatchWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxDispatchWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(_options.Value.PollingIntervalSeconds),
                    stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();
                await processor.ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One cycle failure must not stop the worker permanently.
                _logger.LogError(ex, "OutboxDispatchWorker encountered an error in processing cycle.");
            }
        }

        _logger.LogInformation("OutboxDispatchWorker stopped.");
    }
}

public static class OutboxServiceCollectionExtensions
{
    /// <summary>
    /// Registers the outbox repository over <typeparamref name="TContext"/>, the processor and the
    /// polling worker. Register one <see cref="IOutboxMessageHandler"/> per outbox message type.
    /// </summary>
    public static IServiceCollection AddEComOutbox<TContext>(this IServiceCollection services, IConfiguration configuration)
        where TContext : DbContext
    {
        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
        services.AddScoped<IOutboxRepository, OutboxRepository<TContext>>();
        services.AddScoped<IOutboxProcessor, OutboxProcessor>();
        services.AddHostedService<OutboxDispatchWorker>();
        return services;
    }
}
