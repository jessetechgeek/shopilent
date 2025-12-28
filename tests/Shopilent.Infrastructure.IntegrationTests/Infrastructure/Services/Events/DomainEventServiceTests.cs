using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Events;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Identity.Events;
using Shopilent.Domain.Outbox.Repositories.Read;
using Shopilent.Domain.Sales.Enums;
using Shopilent.Domain.Sales.Events;
using Shopilent.Infrastructure.IntegrationTests.Common;

namespace Shopilent.Infrastructure.IntegrationTests.Infrastructure.Services.Events;

[Collection("IntegrationTests")]
public class DomainEventServiceTests : IntegrationTestBase
{
    private IDomainEventService _domainEventService = null!;
    private IUnitOfWork _unitOfWork = null!;
    private IOutboxMessageReadRepository _outboxMessageReadRepository = null!;
    private ILogger<IDomainEventService> _logger = null!;

    public DomainEventServiceTests(IntegrationTestFixture integrationTestFixture)
        : base(integrationTestFixture)
    {
    }

    protected override Task InitializeTestServices()
    {
        _domainEventService = GetService<IDomainEventService>();
        _unitOfWork = GetService<IUnitOfWork>();
        _outboxMessageReadRepository = GetService<IOutboxMessageReadRepository>();
        _logger = GetService<ILogger<IDomainEventService>>();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task PublishAsync_WithValidDomainEvent_ShouldPublishEventThroughMediator()
    {
        // Arrange
        await ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var domainEvent = new UserCreatedEvent(userId);

        // Act
        var action = () => _domainEventService.PublishAsync(domainEvent);

        // Assert - Should not throw exception
        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishAsync_WithNullDomainEvent_ShouldThrowException()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act & Assert
        var action = () => _domainEventService.PublishAsync(null!);
        await action.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task ProcessEventAsync_WithValidDomainEvent_ShouldCreateOutboxMessage()
    {
        // Arrange
        await ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var domainEvent = new UserCreatedEvent(userId);

        // Act
        await _domainEventService.ProcessEventAsync(domainEvent);
        await _unitOfWork.SaveChangesAsync();

        // Assert - Verify outbox message was created
        var outboxMessages = await _outboxMessageReadRepository.GetAllAsync();
        outboxMessages.Should().HaveCount(1);

        var outboxMessage = outboxMessages.First();
        outboxMessage.Should().NotBeNull();
        outboxMessage.Type.Should().Contain("UserCreatedEvent");
        outboxMessage.Content.Should().NotBeNullOrEmpty();
        outboxMessage.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        outboxMessage.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public async Task ProcessEventAsync_WithDifferentEventTypes_ShouldCreateOutboxMessagesForAll()
    {
        // Arrange
        await ResetDatabaseAsync();

        var addressEvent = new UserCreatedEvent(Guid.NewGuid());
        var orderEvent = new OrderStatusChangedEvent(Guid.NewGuid(), OrderStatus.Pending, OrderStatus.Processing);

        // Act
        await _domainEventService.ProcessEventAsync(addressEvent);
        await _domainEventService.ProcessEventAsync(orderEvent);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var outboxMessages = await _outboxMessageReadRepository.GetAllAsync();
        outboxMessages.Should().HaveCount(2);

        var userOutboxMessage = outboxMessages.FirstOrDefault(m => m.Type.Contains("UserCreatedEvent"));
        var orderOutboxMessage = outboxMessages.FirstOrDefault(m => m.Type.Contains("OrderStatusChangedEvent"));

        userOutboxMessage.Should().NotBeNull();
        orderOutboxMessage.Should().NotBeNull();

        userOutboxMessage!.Content.Should().NotBeNullOrEmpty();
        orderOutboxMessage!.Content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ProcessEventAsync_WithCancellationToken_ShouldHandleCancellation()
    {
        // Arrange
        await ResetDatabaseAsync();

        var domainEvent = new UserCreatedEvent(Guid.NewGuid());
        var cancellationTokenSource = new CancellationTokenSource();

        // Act - Cancel immediately
        cancellationTokenSource.Cancel();

        // This should still work as the current implementation doesn't use the cancellation token
        var action = () => _domainEventService.ProcessEventAsync(domainEvent, cancellationTokenSource.Token);
        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ProcessEventAsync_WithNullDomainEvent_ShouldThrowException()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act & Assert
        var action = () => _domainEventService.ProcessEventAsync(null!);
        await action.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task ProcessEventAsync_MultipleEvents_ShouldCreateSeparateOutboxMessages()
    {
        // Arrange
        await ResetDatabaseAsync();

        var event1 = new UserCreatedEvent(Guid.NewGuid());
        var event2 = new UserCreatedEvent(Guid.NewGuid());
        var event3 = new UserCreatedEvent(Guid.NewGuid());

        // Act
        await _domainEventService.ProcessEventAsync(event1);
        await _domainEventService.ProcessEventAsync(event2);
        await _domainEventService.ProcessEventAsync(event3);
        await _unitOfWork.SaveChangesAsync();

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
    public async Task ProcessEventAsync_SameEventMultipleTimes_ShouldCreateMultipleOutboxMessages()
    {
        // Arrange
        await ResetDatabaseAsync();

        var domainEvent = new UserCreatedEvent(Guid.NewGuid());

        // Act - Process the same event multiple times
        await _domainEventService.ProcessEventAsync(domainEvent);
        await _domainEventService.ProcessEventAsync(domainEvent);
        await _unitOfWork.SaveChangesAsync();

        // Assert - Should create separate outbox messages for each call
        var outboxMessages = await _outboxMessageReadRepository.GetAllAsync();
        outboxMessages.Should().HaveCount(2);

        // Each should have unique IDs even though the event data is the same
        var ids = outboxMessages.Select(m => m.Id).ToList();
        ids.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task ProcessEventAsync_WithComplexEvent_ShouldSerializeEventDataCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();

        var orderId = Guid.NewGuid();
        var fromStatus = OrderStatus.Pending;
        var toStatus = OrderStatus.Processing;
        var domainEvent = new OrderStatusChangedEvent(orderId, fromStatus, toStatus);

        // Act
        await _domainEventService.ProcessEventAsync(domainEvent);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var outboxMessages = await _outboxMessageReadRepository.GetAllAsync();
        outboxMessages.Should().HaveCount(1);

        var outboxMessage = outboxMessages.First();
        outboxMessage.Type.Should().Contain("OrderStatusChangedEvent");

        // The content should contain the serialized event data
        outboxMessage.Content.Should().Contain(orderId.ToString());
        outboxMessage.Content.Should().Contain(((int)fromStatus).ToString()); // OrderStatus.Pending = 0
        outboxMessage.Content.Should().Contain(((int)toStatus).ToString());   // OrderStatus.Processing = 1
    }

    [Fact]
    public async Task ProcessEventAsync_WithoutSaveChanges_ShouldNotPersistToDatabase()
    {
        // Arrange
        await ResetDatabaseAsync();

        var domainEvent = new UserCreatedEvent(Guid.NewGuid());

        // Act - Process event but don't save changes
        await _domainEventService.ProcessEventAsync(domainEvent);
        // Intentionally NOT calling SaveChangesAsync()

        // Assert - Should not be persisted to database yet
        var outboxMessages = await _outboxMessageReadRepository.GetAllAsync();
        outboxMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task PublishAsync_AndProcessEventAsync_ShouldWorkIndependently()
    {
        // Arrange
        await ResetDatabaseAsync();

        var publishEvent = new UserCreatedEvent(Guid.NewGuid());
        var processEvent = new UserCreatedEvent(Guid.NewGuid());

        // Act
        await _domainEventService.PublishAsync(publishEvent); // Direct publish through mediator
        await _domainEventService.ProcessEventAsync(processEvent); // Add to outbox
        await _unitOfWork.SaveChangesAsync();

        // Assert - Only the processed event should be in outbox
        var outboxMessages = await _outboxMessageReadRepository.GetAllAsync();
        outboxMessages.Should().HaveCount(1);

        var outboxMessage = outboxMessages.First();
        outboxMessage.Content.Should().Contain(processEvent.UserId.ToString());
    }

    [Fact]
    public async Task DomainEventService_WithDependencyInjection_ShouldResolveAllDependencies()
    {
        // Arrange & Act
        await ResetDatabaseAsync();

        // The service should be properly constructed through DI
        _domainEventService.Should().NotBeNull();
        _unitOfWork.Should().NotBeNull();
        _logger.Should().NotBeNull();

        // Test that all dependencies work together
        var domainEvent = new UserCreatedEvent(Guid.NewGuid());

        var publishAction = () => _domainEventService.PublishAsync(domainEvent);
        var processAction = () => _domainEventService.ProcessEventAsync(domainEvent);

        // Assert
        await publishAction.Should().NotThrowAsync();
        await processAction.Should().NotThrowAsync();
    }
}
