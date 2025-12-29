using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Outbox;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Outbox;
using Shopilent.Domain.Outbox.Repositories.Write;

namespace Shopilent.Infrastructure.Services.Outbox;

public class OutboxService : IOutboxService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<OutboxService> _logger;

    public OutboxService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<OutboxService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public async Task PublishAsync<T>(T message, DateTime? scheduledAt = null,
        CancellationToken cancellationToken = default) where T : class
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var outboxMessageWriteRepository = scope.ServiceProvider.GetRequiredService<IOutboxMessageWriteRepository>();

        var outboxMessage = OutboxMessage.Create(message, scheduledAt);
        await outboxMessageWriteRepository.AddAsync(outboxMessage, cancellationToken);

        // Save changes
        await unitOfWork.CommitAsync(cancellationToken);
    }

    public async Task ProcessMessagesAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var outboxMessageWriteRepository = scope.ServiceProvider.GetRequiredService<IOutboxMessageWriteRepository>();
        var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IPublisher>();

        const int batchSize = 50;
        var messages = await outboxMessageWriteRepository.GetUnprocessedMessagesAsync(batchSize, cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                // Deserialize and publish the message
                var messageType = ResolveActualType(message.Type);
                if (messageType == null)
                {
                    _logger.LogWarning("Cannot find type {MessageType} for outbox message {MessageId}",
                        message.Type, message.Id);
                    await outboxMessageWriteRepository.MarkAsFailedAsync(message.Id,
                        $"Cannot find type {message.Type}", cancellationToken);
                    continue;
                }

                var typedMessage = JsonSerializer.Deserialize(message.Content, messageType);
                if (typedMessage == null)
                {
                    await outboxMessageWriteRepository.MarkAsFailedAsync(message.Id,
                        "Failed to deserialize message content", cancellationToken);
                    continue;
                }

                // Publish the message
                await mediator.Publish(typedMessage, cancellationToken);

                // Mark as processed
                await outboxMessageWriteRepository.MarkAsProcessedAsync(message.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox message {MessageId}", message.Id);
                await outboxMessageWriteRepository.MarkAsFailedAsync(message.Id, ex.Message, cancellationToken);

                // Exponential backoff for retries
                var retryDelay = TimeSpan.FromMinutes(Math.Pow(2, Math.Min(message.RetryCount, 6)));
                message.Reschedule(retryDelay);
                await unitOfWork.CommitAsync(cancellationToken);
            }
        }
    }

    public async Task CleanupOldMessagesAsync(int daysToKeep = 7, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var outboxMessageWriteRepository = scope.ServiceProvider.GetRequiredService<IOutboxMessageWriteRepository>();


        var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);
        var deletedCount = await outboxMessageWriteRepository.DeleteProcessedMessagesAsync(cutoffDate, cancellationToken);

        _logger.LogInformation("Deleted {DeletedCount} processed outbox messages older than {CutoffDate}",
            deletedCount, cutoffDate);
    }

    private Type ResolveActualType(string simplifiedType)
    {
        // Handle the domain event case
        if (simplifiedType.StartsWith("Event:"))
        {
            string eventName = simplifiedType.Substring("Event:".Length);

            // Find the actual event type in all loaded assemblies
            var eventType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.Name == eventName &&
                                     (t.Namespace?.Contains("Events") == true));

            if (eventType != null)
            {
                // Now construct the DomainEventNotification<TEvent> type
                var notificationType = typeof(Application.Common.Models.DomainEventNotification<>)
                    .MakeGenericType(eventType);

                return notificationType;
            }
        }

        // For non-domain events or fallback
        return Type.GetType(simplifiedType) ??
               AppDomain.CurrentDomain.GetAssemblies()
                   .SelectMany(a => a.GetTypes())
                   .FirstOrDefault(t => t.FullName == simplifiedType);
    }
}
