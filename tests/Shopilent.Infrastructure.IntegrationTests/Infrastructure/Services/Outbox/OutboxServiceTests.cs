using Shopilent.Application.Abstractions.Outbox;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Application.Common.Models;
using Shopilent.Domain.Identity.Events;
using Shopilent.Domain.Outbox.Repositories.Read;
using Shopilent.Domain.Shipping.Events;
using Shopilent.Infrastructure.IntegrationTests.Common;

namespace Shopilent.Infrastructure.IntegrationTests.Infrastructure.Services.Outbox;

[Collection("IntegrationTests")]
public class OutboxServiceTests : IntegrationTestBase
{
    private IOutboxService _outboxService = null!;
    private IUnitOfWork _unitOfWork = null!;
    private IOutboxMessageReadRepository _outboxMessageReadRepository = null!;

    public OutboxServiceTests(IntegrationTestFixture integrationTestFixture)
        : base(integrationTestFixture)
    {
    }

    protected override Task InitializeTestServices()
    {
        _outboxService = GetService<IOutboxService>();
        _unitOfWork = GetService<IUnitOfWork>();
        _outboxMessageReadRepository = GetService<IOutboxMessageReadRepository>();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task PublishAsync_WithValidMessage_ShouldCreateOutboxMessage()
    {
        // Arrange
        await ResetDatabaseAsync();

        var testMessage = new DomainEventNotification<UserCreatedEvent>(new UserCreatedEvent(Guid.NewGuid()));

        // Act
        await _outboxService.PublishAsync(testMessage);

        // Assert
        var outboxMessages = await _outboxMessageReadRepository.GetAllAsync();
        outboxMessages.Should().HaveCount(1);

        var outboxMessage = outboxMessages.First();
        outboxMessage.Should().NotBeNull();
        outboxMessage.Type.Should().NotBeNullOrEmpty();
        outboxMessage.Content.Should().NotBeNullOrEmpty();
        outboxMessage.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        outboxMessage.ProcessedAt.Should().BeNull();
        outboxMessage.ScheduledAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task PublishAsync_WithScheduledTime_ShouldSetScheduledAt()
    {
        // Arrange
        await ResetDatabaseAsync();

        var testMessage = new DomainEventNotification<UserCreatedEvent>(new UserCreatedEvent(Guid.NewGuid()));
        var scheduledTime = DateTime.UtcNow.AddHours(1);

        // Act
        await _outboxService.PublishAsync(testMessage, scheduledTime);

        // Assert
        var outboxMessages = await _outboxMessageReadRepository.GetAllAsync();
        outboxMessages.Should().HaveCount(1);

        var outboxMessage = outboxMessages.First();
        outboxMessage.ScheduledAt.Should().BeCloseTo(scheduledTime, TimeSpan.FromSeconds(1));
        outboxMessage.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public async Task PublishAsync_WithCancellationToken_ShouldHandleCancellation()
    {
        // Arrange
        await ResetDatabaseAsync();

        var testMessage = new DomainEventNotification<UserCreatedEvent>(new UserCreatedEvent(Guid.NewGuid()));
        var cancellationTokenSource = new CancellationTokenSource();

        // Act - Cancel before operation completes
        cancellationTokenSource.Cancel();

        var action = () => _outboxService.PublishAsync(testMessage, cancellationToken: cancellationTokenSource.Token);

        // Assert - Should handle cancellation gracefully
        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task PublishAsync_WithNullMessage_ShouldThrowException()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act & Assert
        var action = () => _outboxService.PublishAsync<AddressCreatedEvent>(null!);
        await action.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task PublishAsync_MultipleMessages_ShouldCreateSeparateOutboxMessages()
    {
        // Arrange
        await ResetDatabaseAsync();

        var message1 = new DomainEventNotification<UserCreatedEvent>(new UserCreatedEvent(Guid.NewGuid()));
        var message2 = new DomainEventNotification<UserCreatedEvent>(new UserCreatedEvent(Guid.NewGuid()));
        var message3 = new DomainEventNotification<UserCreatedEvent>(new UserCreatedEvent(Guid.NewGuid()));

        // Act
        await _outboxService.PublishAsync(message1);
        await _outboxService.PublishAsync(message2);
        await _outboxService.PublishAsync(message3);

        // Assert
        var outboxMessages = await _outboxMessageReadRepository.GetAllAsync();
        outboxMessages.Should().HaveCount(3);

        // Each should have unique IDs
        var ids = outboxMessages.Select(m => m.Id).ToList();
        ids.Should().OnlyHaveUniqueItems();

        // All should be unprocessed
        outboxMessages.Should().AllSatisfy(m => m.ProcessedAt.Should().BeNull());
    }

    [Fact]
    public async Task ProcessMessagesAsync_WithUnprocessedMessages_ShouldProcessThem()
    {
        // Arrange
        await ResetDatabaseAsync();

        var testMessage = new DomainEventNotification<UserCreatedEvent>(new UserCreatedEvent(Guid.NewGuid()));
        await _outboxService.PublishAsync(testMessage);

        // Verify message is unprocessed
        var unprocessedMessages = await _outboxMessageReadRepository.GetUnprocessedMessagesAsync(10);
        unprocessedMessages.Should().HaveCount(1);

        // Act
        await _outboxService.ProcessMessagesAsync();

        // Assert
        var processedMessages = await _outboxMessageReadRepository.GetAllAsync();
        processedMessages.Should().HaveCount(1);

        var processedMessage = processedMessages.First();
        processedMessage.ProcessedAt.Should().NotBeNull();
        processedMessage.ProcessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task ProcessMessagesAsync_WithNoMessages_ShouldNotThrow()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act
        var action = () => _outboxService.ProcessMessagesAsync();

        // Assert - Should not throw when no messages exist
        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ProcessMessagesAsync_WithCancellationToken_ShouldHandleCancellation()
    {
        // Arrange
        await ResetDatabaseAsync();

        var cancellationTokenSource = new CancellationTokenSource();

        // Act - Cancel immediately
        cancellationTokenSource.Cancel();

        var action = () => _outboxService.ProcessMessagesAsync(cancellationTokenSource.Token);

        // Assert - Should handle cancellation
        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ProcessMessagesAsync_WithAlreadyProcessedMessages_ShouldSkipThem()
    {
        // Arrange
        await ResetDatabaseAsync();

        var testMessage = new DomainEventNotification<UserCreatedEvent>(new UserCreatedEvent(Guid.NewGuid()));
        await _outboxService.PublishAsync(testMessage);

        // Process messages once
        await _outboxService.ProcessMessagesAsync();

        // Get the processed message
        var messagesAfterFirstProcess = await _outboxMessageReadRepository.GetAllAsync();
        messagesAfterFirstProcess.Should().HaveCount(1);
        messagesAfterFirstProcess.First().ProcessedAt.Should().NotBeNull();

        // Act - Process again
        await _outboxService.ProcessMessagesAsync();

        // Assert - Should still have only one message, still processed
        var messagesAfterSecondProcess = await _outboxMessageReadRepository.GetAllAsync();
        messagesAfterSecondProcess.Should().HaveCount(1);
        messagesAfterSecondProcess.First().ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CleanupOldMessagesAsync_WithOldProcessedMessages_ShouldDeleteThem()
    {
        // Arrange
        await ResetDatabaseAsync();

        var testMessage = new DomainEventNotification<UserCreatedEvent>(new UserCreatedEvent(Guid.NewGuid()));
        await _outboxService.PublishAsync(testMessage);

        // Verify message was created and is unprocessed initially
        var unprocessedMessages = await _outboxMessageReadRepository.GetUnprocessedMessagesAsync(10);
        unprocessedMessages.Should().HaveCount(1);

        await _outboxService.ProcessMessagesAsync();

        // Verify message exists and is processed
        var messagesBeforeCleanup = await _outboxMessageReadRepository.GetAllAsync();
        messagesBeforeCleanup.Should().HaveCount(1);
        messagesBeforeCleanup.First().ProcessedAt.Should().NotBeNull();

        // Act - Cleanup with 0 days to keep (should delete immediately)
        await _outboxService.CleanupOldMessagesAsync(daysToKeep: 0);

        // Assert - Message should be deleted
        var messagesAfterCleanup = await _outboxMessageReadRepository.GetAllAsync();
        messagesAfterCleanup.Should().BeEmpty();
    }

    [Fact]
    public async Task CleanupOldMessagesAsync_WithRecentProcessedMessages_ShouldKeepThem()
    {
        // Arrange
        await ResetDatabaseAsync();

        var testMessage = new DomainEventNotification<UserCreatedEvent>(new UserCreatedEvent(Guid.NewGuid()));
        await _outboxService.PublishAsync(testMessage);
        await _outboxService.ProcessMessagesAsync();

        // Act - Cleanup with 7 days to keep (recent messages should be kept)
        await _outboxService.CleanupOldMessagesAsync(daysToKeep: 7);

        // Assert - Message should still exist
        var messagesAfterCleanup = await _outboxMessageReadRepository.GetAllAsync();
        messagesAfterCleanup.Should().HaveCount(1);
    }

    [Fact]
    public async Task CleanupOldMessagesAsync_WithUnprocessedMessages_ShouldNotDeleteThem()
    {
        // Arrange
        await ResetDatabaseAsync();

        var testMessage = new DomainEventNotification<UserCreatedEvent>(new UserCreatedEvent(Guid.NewGuid()));
        await _outboxService.PublishAsync(testMessage);
        // Don't process the message - leave it unprocessed

        // Act - Cleanup with 0 days to keep
        await _outboxService.CleanupOldMessagesAsync(daysToKeep: 0);

        // Assert - Unprocessed message should still exist
        var messagesAfterCleanup = await _outboxMessageReadRepository.GetAllAsync();
        messagesAfterCleanup.Should().HaveCount(1);
        messagesAfterCleanup.First().ProcessedAt.Should().BeNull();
    }

    [Fact]
    public async Task CleanupOldMessagesAsync_WithCancellationToken_ShouldHandleCancellation()
    {
        // Arrange
        await ResetDatabaseAsync();

        var cancellationTokenSource = new CancellationTokenSource();

        // Act - Cancel immediately
        cancellationTokenSource.Cancel();

        var action = () => _outboxService.CleanupOldMessagesAsync(cancellationToken: cancellationTokenSource.Token);

        // Assert - Should handle cancellation
        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task OutboxService_EndToEndWorkflow_ShouldWorkCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();

        var message1 = new DomainEventNotification<UserCreatedEvent>(new UserCreatedEvent(Guid.NewGuid()));
        var message2 = new DomainEventNotification<UserCreatedEvent>(new UserCreatedEvent(Guid.NewGuid()));

        // Act - Full workflow: Publish -> Process -> Cleanup

        // 1. Publish messages
        await _outboxService.PublishAsync(message1);
        await _outboxService.PublishAsync(message2);

        var messagesAfterPublish = await _outboxMessageReadRepository.GetAllAsync();
        messagesAfterPublish.Should().HaveCount(2);
        messagesAfterPublish.Should().AllSatisfy(m => m.ProcessedAt.Should().BeNull());

        // 2. Process messages
        await _outboxService.ProcessMessagesAsync();

        var messagesAfterProcess = await _outboxMessageReadRepository.GetAllAsync();
        messagesAfterProcess.Should().HaveCount(2);
        messagesAfterProcess.Should().AllSatisfy(m => m.ProcessedAt.Should().NotBeNull());

        // 3. Cleanup old messages
        await _outboxService.CleanupOldMessagesAsync(daysToKeep: 0);

        var messagesAfterCleanup = await _outboxMessageReadRepository.GetAllAsync();
        messagesAfterCleanup.Should().BeEmpty();
    }

    [Fact]
    public async Task OutboxService_WithDependencyInjection_ShouldResolveAllDependencies()
    {
        // Arrange & Act
        await ResetDatabaseAsync();

        // The service should be properly constructed through DI
        _outboxService.Should().NotBeNull();
        _unitOfWork.Should().NotBeNull();

        // Test that all methods work with dependency injection
        var testMessage = new DomainEventNotification<UserCreatedEvent>(new UserCreatedEvent(Guid.NewGuid()));

        var publishAction = () => _outboxService.PublishAsync(testMessage);
        var processAction = () => _outboxService.ProcessMessagesAsync();
        var cleanupAction = () => _outboxService.CleanupOldMessagesAsync();

        // Assert
        await publishAction.Should().NotThrowAsync();
        await processAction.Should().NotThrowAsync();
        await cleanupAction.Should().NotThrowAsync();
    }
}
