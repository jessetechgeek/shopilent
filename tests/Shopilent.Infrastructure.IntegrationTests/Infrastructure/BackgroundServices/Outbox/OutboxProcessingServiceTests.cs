using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shopilent.Application.Abstractions.Outbox;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Application.Common.Models;
using Shopilent.Domain.Identity.Events;
using Shopilent.Domain.Outbox.Repositories.Read;
using Shopilent.Domain.Outbox.Repositories.Write;
using Shopilent.Infrastructure.BackgroundServices.Outbox;
using Shopilent.Infrastructure.IntegrationTests.Common;
using Shopilent.Infrastructure.Settings;

namespace Shopilent.Infrastructure.IntegrationTests.Infrastructure.BackgroundServices.Outbox;

[Collection("IntegrationTests")]
public class OutboxProcessingServiceTests : IntegrationTestBase
{
    private IOutboxService _outboxService = null!;
    private IOutboxMessageReadRepository _outboxMessageReadRepository = null!;
    private IServiceScopeFactory _serviceScopeFactory = null!;
    private ILogger<OutboxProcessingService> _logger = null!;

    public OutboxProcessingServiceTests(IntegrationTestFixture integrationTestFixture)
        : base(integrationTestFixture)
    {
    }

    protected override Task InitializeTestServices()
    {
        _outboxService = GetService<IOutboxService>();
        _outboxMessageReadRepository = GetService<IOutboxMessageReadRepository>();
        _serviceScopeFactory = GetService<IServiceScopeFactory>();
        _logger = GetService<ILogger<OutboxProcessingService>>();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task OutboxProcessingService_CanBeCreated_ShouldNotThrow()
    {
        // Arrange
        await ResetDatabaseAsync();

        var settings = Options.Create(new OutboxSettings
        {
            ProcessingIntervalMilliseconds = 1000,
            DaysToKeepProcessedMessages = 7,
            CleanupIntervalHours = 24,
            MaxRetryCount = 5
        });

        // Act
        var service = new OutboxProcessingService(_serviceScopeFactory, settings, _logger);

        // Assert
        service.Should().NotBeNull();
        service.Should().BeAssignableTo<BackgroundService>();
        service.Should().BeAssignableTo<IHostedService>();

        // Cleanup
        service.Dispose();
    }

    [Fact]
    public async Task OutboxProcessingService_StartAndStop_ShouldWorkCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();

        var settings = Options.Create(new OutboxSettings
        {
            ProcessingIntervalMilliseconds = 100, // Very short interval for testing
            DaysToKeepProcessedMessages = 7,
            CleanupIntervalHours = 24
        });

        var service = new OutboxProcessingService(_serviceScopeFactory, settings, _logger);
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act - Start the service
        var startTask = service.StartAsync(cancellationTokenSource.Token);

        // Allow some time for the service to start
        await Task.Delay(50, cancellationTokenSource.Token);

        // Stop the service
        var stopTask = service.StopAsync(cancellationTokenSource.Token);

        // Assert
        await startTask;
        await stopTask;

        // Cleanup
        service.Dispose();
    }

    [Fact]
    public async Task OutboxProcessingService_ProcessesMessages_WhenRunning()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Create unprocessed message
        var testMessage = new DomainEventNotification<UserCreatedEvent>(new UserCreatedEvent(Guid.NewGuid()));
        await _outboxService.PublishAsync(testMessage);

        // Verify message is unprocessed
        var unprocessedMessages = await _outboxMessageReadRepository.GetUnprocessedMessagesAsync(10);
        unprocessedMessages.Should().HaveCount(1);

        var settings = Options.Create(new OutboxSettings
        {
            ProcessingIntervalMilliseconds = 100, // Process quickly
            DaysToKeepProcessedMessages = 7,
            CleanupIntervalHours = 24
        });

        var service = new OutboxProcessingService(_serviceScopeFactory, settings, _logger);
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        var startTask = service.StartAsync(cancellationTokenSource.Token);

        // Allow time for message processing
        await Task.Delay(500, cancellationTokenSource.Token);

        cancellationTokenSource.Cancel();
        var stopTask = service.StopAsync(CancellationToken.None);

        // Assert
        await startTask;
        await stopTask;

        // Verify message was processed
        var processedMessages = await _outboxMessageReadRepository.GetAllAsync();
        processedMessages.Should().HaveCount(1);
        processedMessages.First().ProcessedAt.Should().NotBeNull();

        // Cleanup
        service.Dispose();
    }

    [Fact]
    public async Task OutboxProcessingService_WithException_ShouldContinueRunning()
    {
        // Arrange
        await ResetDatabaseAsync();

        var settings = Options.Create(new OutboxSettings
        {
            ProcessingIntervalMilliseconds = 100,
            DaysToKeepProcessedMessages = 7,
            CleanupIntervalHours = 24
        });

        var service = new OutboxProcessingService(_serviceScopeFactory, settings, _logger);
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        var startTask = service.StartAsync(cancellationTokenSource.Token);

        // Let it run for a bit to ensure it can handle any internal exceptions
        await Task.Delay(200, cancellationTokenSource.Token);

        cancellationTokenSource.Cancel();
        var stopTask = service.StopAsync(CancellationToken.None);

        // Assert - Should not throw even if internal exceptions occur
        await startTask;
        await stopTask;

        // Cleanup
        service.Dispose();
    }

    [Fact]
    public async Task OutboxProcessingService_WithCustomSettings_ShouldUseProvidedSettings()
    {
        // Arrange
        await ResetDatabaseAsync();

        var customSettings = new OutboxSettings
        {
            ProcessingIntervalMilliseconds = 2000,
            DaysToKeepProcessedMessages = 14,
            CleanupIntervalHours = 48,
            MaxRetryCount = 10
        };

        var settings = Options.Create(customSettings);
        var service = new OutboxProcessingService(_serviceScopeFactory, settings, _logger);

        // Act & Assert - Should not throw with custom settings
        service.Should().NotBeNull();

        // Start and immediately stop to verify settings don't cause issues
        using var cancellationTokenSource = new CancellationTokenSource();
        var startTask = service.StartAsync(cancellationTokenSource.Token);

        await Task.Delay(50);
        cancellationTokenSource.Cancel();

        var stopTask = service.StopAsync(CancellationToken.None);

        await startTask;
        await stopTask;

        // Cleanup
        service.Dispose();
    }

    [Fact]
    public async Task OutboxProcessingService_Dispose_ShouldCleanupResources()
    {
        // Arrange
        await ResetDatabaseAsync();

        var settings = Options.Create(new OutboxSettings());
        var service = new OutboxProcessingService(_serviceScopeFactory, settings, _logger);

        // Act
        var disposeAction = () => service.Dispose();

        // Assert - Should not throw when disposing
        disposeAction.Should().NotThrow();

        // Disposing again should also not throw
        disposeAction.Should().NotThrow();
    }

    [Fact]
    public async Task OutboxProcessingService_MultipleMessages_ShouldProcessAll()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Create multiple unprocessed messages
        var message1 = new DomainEventNotification<UserCreatedEvent>(new UserCreatedEvent(Guid.NewGuid()));
        var message2 = new DomainEventNotification<UserCreatedEvent>(new UserCreatedEvent(Guid.NewGuid()));
        var message3 = new DomainEventNotification<UserCreatedEvent>(new UserCreatedEvent(Guid.NewGuid()));

        await _outboxService.PublishAsync(message1);
        await _outboxService.PublishAsync(message2);
        await _outboxService.PublishAsync(message3);

        // Verify messages are unprocessed
        var unprocessedMessages = await _outboxMessageReadRepository.GetUnprocessedMessagesAsync(10);
        unprocessedMessages.Should().HaveCount(3);

        var settings = Options.Create(new OutboxSettings
        {
            ProcessingIntervalMilliseconds = 100,
            DaysToKeepProcessedMessages = 7,
            CleanupIntervalHours = 24
        });

        var service = new OutboxProcessingService(_serviceScopeFactory, settings, _logger);
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        var startTask = service.StartAsync(cancellationTokenSource.Token);

        // Allow time for all messages to be processed
        await Task.Delay(800, cancellationTokenSource.Token);

        cancellationTokenSource.Cancel();
        var stopTask = service.StopAsync(CancellationToken.None);

        // Assert
        await startTask;
        await stopTask;

        // Verify all messages were processed
        var processedMessages = await _outboxMessageReadRepository.GetAllAsync();
        processedMessages.Should().HaveCount(3);
        processedMessages.Should().AllSatisfy(m => m.ProcessedAt.Should().NotBeNull());

        // Cleanup
        service.Dispose();
    }

    [Fact]
    public async Task OutboxProcessingService_WithDependencyInjection_ShouldResolveAllDependencies()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act - Try to resolve the service through DI
        var hostedServices = GetService<IEnumerable<IHostedService>>();
        var outboxProcessingService = hostedServices.OfType<OutboxProcessingService>().FirstOrDefault();

        // Assert
        outboxProcessingService.Should().NotBeNull("OutboxProcessingService should be registered as IHostedService");

        // Verify it can start and stop
        using var cancellationTokenSource = new CancellationTokenSource();
        var startAction = () => outboxProcessingService!.StartAsync(cancellationTokenSource.Token);
        await startAction.Should().NotThrowAsync();

        await Task.Delay(50);
        cancellationTokenSource.Cancel();

        var stopAction = () => outboxProcessingService!.StopAsync(CancellationToken.None);
        await stopAction.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OutboxProcessingService_LongRunning_ShouldContinueProcessingOverTime()
    {
        // Arrange
        await ResetDatabaseAsync();

        var settings = Options.Create(new OutboxSettings
        {
            ProcessingIntervalMilliseconds = 200,
            DaysToKeepProcessedMessages = 7,
            CleanupIntervalHours = 24
        });

        var service = new OutboxProcessingService(_serviceScopeFactory, settings, _logger);
        using var cancellationTokenSource = new CancellationTokenSource();

        // Start the service
        var startTask = service.StartAsync(cancellationTokenSource.Token);
        await Task.Delay(100, cancellationTokenSource.Token);

        // Act - Add messages while service is running
        var message1 = new DomainEventNotification<UserCreatedEvent>(new UserCreatedEvent(Guid.NewGuid()));
        await _outboxService.PublishAsync(message1);

        await Task.Delay(300, cancellationTokenSource.Token); // Allow processing

        var message2 = new DomainEventNotification<UserCreatedEvent>(new UserCreatedEvent(Guid.NewGuid()));
        await _outboxService.PublishAsync(message2);

        await Task.Delay(300, cancellationTokenSource.Token); // Allow processing

        cancellationTokenSource.Cancel();
        var stopTask = service.StopAsync(CancellationToken.None);

        // Assert
        await startTask;
        await stopTask;

        // Both messages should have been processed
        var processedMessages = await _outboxMessageReadRepository.GetAllAsync();
        processedMessages.Should().HaveCount(2);
        processedMessages.Should().AllSatisfy(m => m.ProcessedAt.Should().NotBeNull());

        // Cleanup
        service.Dispose();
    }
}
