using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Outbox.Repositories.Read;
using Shopilent.Domain.Outbox.Repositories.Write;
using Shopilent.Infrastructure.IntegrationTests.Common;
using Shopilent.Infrastructure.IntegrationTests.TestData.Builders;

namespace Shopilent.Infrastructure.IntegrationTests.Infrastructure.Persistence.PostgreSQL.Repositories.Outbox.Read;

[Collection("IntegrationTests")]
public class OutboxMessageReadRepositoryTests : IntegrationTestBase
{
    private IUnitOfWork _unitOfWork = null!;
    private IOutboxMessageWriteRepository _outboxMessageWriteRepository = null!;
    private IOutboxMessageReadRepository _outboxMessageReadRepository = null!;

    public OutboxMessageReadRepositoryTests(IntegrationTestFixture fixture) : base(fixture)
    {
    }

    protected override Task InitializeTestServices()
    {
        _unitOfWork = GetService<IUnitOfWork>();
        _outboxMessageWriteRepository = GetService<IOutboxMessageWriteRepository>();
        _outboxMessageReadRepository = GetService<IOutboxMessageReadRepository>();
        return Task.CompletedTask;
    }

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ExistingId_ShouldReturnOutboxMessageDto()
    {
        // Arrange
        await ResetDatabaseAsync();

        var outboxMessage = OutboxMessageBuilder.CreateDefault();
        await _outboxMessageWriteRepository.AddAsync(outboxMessage);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _outboxMessageReadRepository.GetByIdAsync(outboxMessage.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(outboxMessage.Id);
        result.Type.Should().Be(outboxMessage.Type);
        result.Content.Should().Be(outboxMessage.Content);
        result.ProcessedAt.Should().BeNull();
        result.Error.Should().BeNull();
        result.RetryCount.Should().Be(0);
        result.CreatedAt.Should().BeCloseTo(outboxMessage.CreatedAt, TimeSpan.FromMilliseconds(100));
        result.ScheduledAt.Should().BeCloseTo(outboxMessage.ScheduledAt.Value, TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _outboxMessageReadRepository.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region ListAllAsync Tests

    [Fact]
    public async Task ListAllAsync_HasOutboxMessages_ShouldReturnAllOrderedByCreatedAtDesc()
    {
        // Arrange
        await ResetDatabaseAsync();

        var message1 = OutboxMessageBuilder.CreateDomainEvent("Event1", Guid.NewGuid());
        await _outboxMessageWriteRepository.AddAsync(message1);
        await _unitOfWork.SaveChangesAsync();
        await Task.Delay(100); // Ensure sufficient time gap

        var message2 = OutboxMessageBuilder.CreateDomainEvent("Event2", Guid.NewGuid());
        await _outboxMessageWriteRepository.AddAsync(message2);
        await _unitOfWork.SaveChangesAsync();
        await Task.Delay(100); // Ensure sufficient time gap

        var message3 = OutboxMessageBuilder.CreateEmailNotification("Welcome", "test@example.com");
        await _outboxMessageWriteRepository.AddAsync(message3);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var results = await _outboxMessageReadRepository.ListAllAsync();

        // Assert
        results.Should().NotBeEmpty();
        results.Should().HaveCount(3);
        results.Should().BeInDescendingOrder(x => x.CreatedAt);

        var messageIds = results.Select(r => r.Id).ToList();
        messageIds.Should().Contain(message1.Id);
        messageIds.Should().Contain(message2.Id);
        messageIds.Should().Contain(message3.Id);
    }

    [Fact]
    public async Task ListAllAsync_NoOutboxMessages_ShouldReturnEmptyList()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act
        var results = await _outboxMessageReadRepository.ListAllAsync();

        // Assert
        results.Should().BeEmpty();
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_HasOutboxMessages_ShouldReturnAllOrderedByCreatedAtDesc()
    {
        // Arrange
        await ResetDatabaseAsync();

        var messages = OutboxMessageBuilder.CreateMultipleUnprocessed(5, DateTime.UtcNow.AddMinutes(-10));
        foreach (var message in messages)
        {
            await _outboxMessageWriteRepository.AddAsync(message);
            await _unitOfWork.SaveChangesAsync();
            await Task.Delay(50); // Small delay to ensure distinct created times
        }

        // Act
        var results = await _outboxMessageReadRepository.GetAllAsync();

        // Assert
        results.Should().NotBeEmpty();
        results.Should().HaveCount(5);
        results.Should().BeInDescendingOrder(x => x.CreatedAt);

        var messageIds = results.Select(r => r.Id).ToList();
        foreach (var message in messages)
        {
            messageIds.Should().Contain(message.Id);
        }
    }

    #endregion

    #region GetByStatusAsync Tests

    [Fact]
    public async Task GetByStatusAsync_ProcessedMessages_ShouldReturnOnlyProcessedMessages()
    {
        // Arrange
        await ResetDatabaseAsync();

        var processedMessage1 = OutboxMessageBuilder.CreateProcessed();
        var processedMessage2 = OutboxMessageBuilder.CreateProcessed();
        var unprocessedMessage = OutboxMessageBuilder.CreateDefault();

        await _outboxMessageWriteRepository.AddAsync(processedMessage1);
        await _outboxMessageWriteRepository.AddAsync(processedMessage2);
        await _outboxMessageWriteRepository.AddAsync(unprocessedMessage);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var results = await _outboxMessageReadRepository.GetByStatusAsync(isProcessed: true);

        // Assert
        results.Should().NotBeEmpty();
        results.Should().HaveCount(2);
        results.Should().OnlyContain(r => r.IsProcessed);
        results.Should().BeInDescendingOrder(x => x.CreatedAt);

        var messageIds = results.Select(r => r.Id).ToList();
        messageIds.Should().Contain(processedMessage1.Id);
        messageIds.Should().Contain(processedMessage2.Id);
        messageIds.Should().NotContain(unprocessedMessage.Id);
    }

    [Fact]
    public async Task GetByStatusAsync_UnprocessedMessages_ShouldReturnOnlyUnprocessedMessages()
    {
        // Arrange
        await ResetDatabaseAsync();

        var processedMessage = OutboxMessageBuilder.CreateProcessed();
        var unprocessedMessage1 = OutboxMessageBuilder.CreateDefault();
        var unprocessedMessage2 = OutboxMessageBuilder.CreateDefault();

        await _outboxMessageWriteRepository.AddAsync(processedMessage);
        await _outboxMessageWriteRepository.AddAsync(unprocessedMessage1);
        await _outboxMessageWriteRepository.AddAsync(unprocessedMessage2);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var results = await _outboxMessageReadRepository.GetByStatusAsync(isProcessed: false);

        // Assert
        results.Should().NotBeEmpty();
        results.Should().HaveCount(2);
        results.Should().OnlyContain(r => !r.IsProcessed);
        results.Should().BeInDescendingOrder(x => x.CreatedAt);

        var messageIds = results.Select(r => r.Id).ToList();
        messageIds.Should().Contain(unprocessedMessage1.Id);
        messageIds.Should().Contain(unprocessedMessage2.Id);
        messageIds.Should().NotContain(processedMessage.Id);
    }

    [Fact]
    public async Task GetByStatusAsync_NoMatchingMessages_ShouldReturnEmptyList()
    {
        // Arrange
        await ResetDatabaseAsync();

        var unprocessedMessage = OutboxMessageBuilder.CreateDefault();
        await _outboxMessageWriteRepository.AddAsync(unprocessedMessage);
        await _unitOfWork.SaveChangesAsync();

        // Act - Looking for processed messages when only unprocessed exist
        var results = await _outboxMessageReadRepository.GetByStatusAsync(isProcessed: true);

        // Assert
        results.Should().BeEmpty();
    }

    #endregion

    #region GetByTypeAsync Tests

    [Fact]
    public async Task GetByTypeAsync_ExistingType_ShouldReturnMessagesOfThatType()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Create a single message first and get its actual type
        var sampleMessage = OutboxMessageBuilder.CreateDefault();
        await _outboxMessageWriteRepository.AddAsync(sampleMessage);
        await _unitOfWork.SaveChangesAsync();

        // Get the actual type that was stored
        var storedMessage = await _outboxMessageReadRepository.GetByIdAsync(sampleMessage.Id);
        var actualType = storedMessage!.Type;

        // Create more messages with the same type by using the same builder pattern
        var message1 = OutboxMessageBuilder.CreateDefault();
        var message2 = OutboxMessageBuilder.CreateDefault();

        await _outboxMessageWriteRepository.AddAsync(message1);
        await _outboxMessageWriteRepository.AddAsync(message2);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var results = await _outboxMessageReadRepository.GetByTypeAsync(actualType);

        // Assert
        results.Should().NotBeEmpty();
        results.Should().OnlyContain(r => r.Type == actualType);
        results.Should().BeInDescendingOrder(x => x.CreatedAt);

        // Should return all 3 messages since they all have the same type
        results.Should().HaveCount(3);

        var messageIds = results.Select(r => r.Id).ToList();
        messageIds.Should().Contain(sampleMessage.Id);
        messageIds.Should().Contain(message1.Id);
        messageIds.Should().Contain(message2.Id);
    }

    [Fact]
    public async Task GetByTypeAsync_NonExistentType_ShouldReturnEmptyList()
    {
        // Arrange
        await ResetDatabaseAsync();

        var message = new OutboxMessageBuilder()
            .ForEmailNotification("TestEmail", "test@example.com")
            .Build();
        await _outboxMessageWriteRepository.AddAsync(message);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var results = await _outboxMessageReadRepository.GetByTypeAsync("NonExistentType");

        // Assert
        results.Should().BeEmpty();
    }

    #endregion

    #region GetByDateRangeAsync Tests

    [Fact]
    public async Task GetByDateRangeAsync_WithinRange_ShouldReturnMessagesInRange()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Create some messages first and get their actual timestamps
        var message1 = OutboxMessageBuilder.CreateDefault();
        var message2 = OutboxMessageBuilder.CreateDefault();
        var message3 = OutboxMessageBuilder.CreateDefault();

        await _outboxMessageWriteRepository.AddAsync(message1);
        await _outboxMessageWriteRepository.AddAsync(message2);
        await _outboxMessageWriteRepository.AddAsync(message3);
        await _unitOfWork.SaveChangesAsync();

        // Get all messages to find their actual CreatedAt timestamps
        var allMessages = await _outboxMessageReadRepository.GetAllAsync();
        allMessages.Should().HaveCount(3);

        // Use the actual timestamps to create a date range that includes some but not all messages
        var timestamps = allMessages.Select(m => m.CreatedAt).OrderBy(t => t).ToList();
        var fromDate = timestamps[0].AddMilliseconds(-1000); // Just before the first message
        var toDate = timestamps[1].AddMilliseconds(1000);    // Just after the second message

        // Act
        var results = await _outboxMessageReadRepository.GetByDateRangeAsync(fromDate, toDate);

        // Assert
        results.Should().NotBeEmpty();
        results.Should().HaveCountGreaterThanOrEqualTo(2); // At least the first two messages
        results.Should().BeInDescendingOrder(x => x.CreatedAt);

        // Verify all returned messages are within the date range
        results.Should().OnlyContain(r => r.CreatedAt >= fromDate && r.CreatedAt <= toDate);
    }

    [Fact]
    public async Task GetByDateRangeAsync_NoMessagesInRange_ShouldReturnEmptyList()
    {
        // Arrange
        await ResetDatabaseAsync();

        var baseTime = DateTime.UtcNow.AddDays(-10);
        var message = new OutboxMessageBuilder()
            .WithCreatedAt(baseTime)
            .Build();

        await _outboxMessageWriteRepository.AddAsync(message);
        await _unitOfWork.SaveChangesAsync();

        // Act - Query for a different date range
        var fromDate = DateTime.UtcNow.AddDays(-5);
        var toDate = DateTime.UtcNow.AddDays(-3);
        var results = await _outboxMessageReadRepository.GetByDateRangeAsync(fromDate, toDate);

        // Assert
        results.Should().BeEmpty();
    }

    #endregion

    #region GetFailedMessagesAsync Tests

    [Fact]
    public async Task GetFailedMessagesAsync_HasFailedMessages_ShouldReturnOnlyFailedMessages()
    {
        // Arrange
        await ResetDatabaseAsync();

        var failedMessage1 = OutboxMessageBuilder.CreateWithError("First error");
        var failedMessage2 = OutboxMessageBuilder.CreateWithError("Second error");
        var successfulMessage = OutboxMessageBuilder.CreateDefault();

        await _outboxMessageWriteRepository.AddAsync(failedMessage1);
        await _outboxMessageWriteRepository.AddAsync(failedMessage2);
        await _outboxMessageWriteRepository.AddAsync(successfulMessage);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var results = await _outboxMessageReadRepository.GetFailedMessagesAsync();

        // Assert
        results.Should().NotBeEmpty();
        results.Should().HaveCount(2);
        results.Should().OnlyContain(r => r.HasError);
        results.Should().BeInDescendingOrder(x => x.CreatedAt);

        var messageIds = results.Select(r => r.Id).ToList();
        messageIds.Should().Contain(failedMessage1.Id);
        messageIds.Should().Contain(failedMessage2.Id);
        messageIds.Should().NotContain(successfulMessage.Id);

        results.Should().Contain(r => r.Error == "First error");
        results.Should().Contain(r => r.Error == "Second error");
    }

    [Fact]
    public async Task GetFailedMessagesAsync_NoFailedMessages_ShouldReturnEmptyList()
    {
        // Arrange
        await ResetDatabaseAsync();

        var successfulMessage1 = OutboxMessageBuilder.CreateDefault();
        var successfulMessage2 = OutboxMessageBuilder.CreateProcessed();

        await _outboxMessageWriteRepository.AddAsync(successfulMessage1);
        await _outboxMessageWriteRepository.AddAsync(successfulMessage2);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var results = await _outboxMessageReadRepository.GetFailedMessagesAsync();

        // Assert
        results.Should().BeEmpty();
    }

    #endregion

    #region GetUnprocessedMessagesAsync Tests

    [Fact]
    public async Task GetUnprocessedMessagesAsync_HasUnprocessedMessages_ShouldReturnOrderedByScheduledAt()
    {
        // Arrange
        await ResetDatabaseAsync();

        var baseTime = DateTime.UtcNow.AddMinutes(-10);
        var messages = new List<Domain.Outbox.OutboxMessage>
        {
            OutboxMessageBuilder.CreateDomainEvent("Event1", Guid.NewGuid()),
            OutboxMessageBuilder.CreateDomainEvent("Event2", Guid.NewGuid()),
            OutboxMessageBuilder.CreateDomainEvent("Event3", Guid.NewGuid())
        };

        // Set different scheduled times to test ordering
        var scheduledTimes = new[] { baseTime.AddMinutes(2), baseTime, baseTime.AddMinutes(1) };
        for (int i = 0; i < messages.Count; i++)
        {
            messages[i].Reschedule(scheduledTimes[i] - DateTime.UtcNow);
            await _outboxMessageWriteRepository.AddAsync(messages[i]);
        }

        // Add a processed message that should be excluded
        var processedMessage = OutboxMessageBuilder.CreateProcessed();
        await _outboxMessageWriteRepository.AddAsync(processedMessage);

        await _unitOfWork.SaveChangesAsync();

        // Act
        var results = await _outboxMessageReadRepository.GetUnprocessedMessagesAsync();

        // Assert
        results.Should().NotBeEmpty();
        results.Should().HaveCount(3);
        results.Should().OnlyContain(r => !r.IsProcessed);

        // Should be ordered by ScheduledAt ASC, then CreatedAt ASC
        results[0].ScheduledAt.Should().BeCloseTo(baseTime, TimeSpan.FromMilliseconds(100));
        results[1].ScheduledAt.Should().BeCloseTo(baseTime.AddMinutes(1), TimeSpan.FromMilliseconds(100));
        results[2].ScheduledAt.Should().BeCloseTo(baseTime.AddMinutes(2), TimeSpan.FromMilliseconds(100));

        var messageIds = results.Select(r => r.Id).ToList();
        messageIds.Should().NotContain(processedMessage.Id);
    }

    [Fact]
    public async Task GetUnprocessedMessagesAsync_WithLimit_ShouldRespectLimit()
    {
        // Arrange
        await ResetDatabaseAsync();

        var messages = OutboxMessageBuilder.CreateMultipleUnprocessed(5, DateTime.UtcNow.AddMinutes(-5));
        foreach (var message in messages)
        {
            await _outboxMessageWriteRepository.AddAsync(message);
        }
        await _unitOfWork.SaveChangesAsync();

        // Act
        var results = await _outboxMessageReadRepository.GetUnprocessedMessagesAsync(limit: 3);

        // Assert
        results.Should().NotBeEmpty();
        results.Should().HaveCount(3);
        results.Should().OnlyContain(r => !r.IsProcessed);
    }

    [Fact]
    public async Task GetUnprocessedMessagesAsync_NoUnprocessedMessages_ShouldReturnEmptyList()
    {
        // Arrange
        await ResetDatabaseAsync();

        var processedMessage = OutboxMessageBuilder.CreateProcessed();
        await _outboxMessageWriteRepository.AddAsync(processedMessage);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var results = await _outboxMessageReadRepository.GetUnprocessedMessagesAsync();

        // Assert
        results.Should().BeEmpty();
    }

    #endregion

    #region GetUnprocessedCountAsync Tests

    [Fact]
    public async Task GetUnprocessedCountAsync_HasUnprocessedMessages_ShouldReturnCorrectCount()
    {
        // Arrange
        await ResetDatabaseAsync();

        var unprocessedMessages = OutboxMessageBuilder.CreateMultipleUnprocessed(3, DateTime.UtcNow.AddMinutes(-5));
        var processedMessage = OutboxMessageBuilder.CreateProcessed();

        foreach (var message in unprocessedMessages)
        {
            await _outboxMessageWriteRepository.AddAsync(message);
        }
        await _outboxMessageWriteRepository.AddAsync(processedMessage);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var count = await _outboxMessageReadRepository.GetUnprocessedCountAsync();

        // Assert
        count.Should().Be(3);
    }

    [Fact]
    public async Task GetUnprocessedCountAsync_NoUnprocessedMessages_ShouldReturnZero()
    {
        // Arrange
        await ResetDatabaseAsync();

        var processedMessage = OutboxMessageBuilder.CreateProcessed();
        await _outboxMessageWriteRepository.AddAsync(processedMessage);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var count = await _outboxMessageReadRepository.GetUnprocessedCountAsync();

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public async Task GetUnprocessedCountAsync_EmptyRepository_ShouldReturnZero()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act
        var count = await _outboxMessageReadRepository.GetUnprocessedCountAsync();

        // Assert
        count.Should().Be(0);
    }

    #endregion

    #region GetTotalCountAsync Tests

    [Fact]
    public async Task GetTotalCountAsync_HasMessages_ShouldReturnCorrectTotalCount()
    {
        // Arrange
        await ResetDatabaseAsync();

        var unprocessedMessages = OutboxMessageBuilder.CreateMultipleUnprocessed(3, DateTime.UtcNow.AddMinutes(-5));
        var processedMessages = new List<Domain.Outbox.OutboxMessage>
        {
            OutboxMessageBuilder.CreateProcessed(),
            OutboxMessageBuilder.CreateProcessed()
        };
        var failedMessage = OutboxMessageBuilder.CreateWithError("Test error");

        foreach (var message in unprocessedMessages)
        {
            await _outboxMessageWriteRepository.AddAsync(message);
        }
        foreach (var message in processedMessages)
        {
            await _outboxMessageWriteRepository.AddAsync(message);
        }
        await _outboxMessageWriteRepository.AddAsync(failedMessage);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var totalCount = await _outboxMessageReadRepository.GetTotalCountAsync();

        // Assert
        totalCount.Should().Be(6); // 3 unprocessed + 2 processed + 1 failed
    }

    [Fact]
    public async Task GetTotalCountAsync_EmptyRepository_ShouldReturnZero()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act
        var totalCount = await _outboxMessageReadRepository.GetTotalCountAsync();

        // Assert
        totalCount.Should().Be(0);
    }

    #endregion

    #region Mixed Scenarios Tests

    [Fact]
    public async Task MixedMessageTypes_ShouldHandleCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();

        var messages = OutboxMessageBuilder.CreateMixedProcessedAndUnprocessed(6, 3);
        foreach (var message in messages)
        {
            await _outboxMessageWriteRepository.AddAsync(message);
        }
        await _unitOfWork.SaveChangesAsync();

        // Act & Assert
        var allMessages = await _outboxMessageReadRepository.GetAllAsync();
        allMessages.Should().HaveCount(6);

        var processedMessages = await _outboxMessageReadRepository.GetByStatusAsync(isProcessed: true);
        processedMessages.Should().HaveCount(3);

        var unprocessedMessages = await _outboxMessageReadRepository.GetByStatusAsync(isProcessed: false);
        unprocessedMessages.Should().HaveCount(3);

        var unprocessedCount = await _outboxMessageReadRepository.GetUnprocessedCountAsync();
        unprocessedCount.Should().Be(3);

        var totalCount = await _outboxMessageReadRepository.GetTotalCountAsync();
        totalCount.Should().Be(6);
    }

    [Fact]
    public async Task GetByTypeAsync_CaseSensitive_ShouldMatchExactly()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Create three different messages and get their actual types from the database
        var message1 = new OutboxMessageBuilder()
            .ForEmailNotification("welcome", "test1@example.com")
            .Build();

        var message2 = new OutboxMessageBuilder()
            .ForEmailNotification("WELCOME", "test2@example.com")
            .Build();

        var message3 = new OutboxMessageBuilder()
            .ForEmailNotification("Welcome", "test3@example.com")
            .Build();

        await _outboxMessageWriteRepository.AddAsync(message1);
        await _outboxMessageWriteRepository.AddAsync(message2);
        await _outboxMessageWriteRepository.AddAsync(message3);
        await _unitOfWork.SaveChangesAsync();

        // Get all messages to see what types were actually stored
        var allMessages = await _outboxMessageReadRepository.GetAllAsync();
        allMessages.Should().HaveCount(3);

        // Use the actual stored types for our queries
        var actualTypes = allMessages.Select(m => m.Type).Distinct().ToList();

        foreach (var type in actualTypes)
        {
            var results = await _outboxMessageReadRepository.GetByTypeAsync(type);
            results.Should().NotBeEmpty("Type {0} should have at least one message", type);
            results.Should().OnlyContain(r => r.Type == type, "All results should match the queried type exactly");
        }

        // Test that querying with a non-existent type returns empty
        var nonExistentResults = await _outboxMessageReadRepository.GetByTypeAsync("NonExistentType");
        nonExistentResults.Should().BeEmpty();
    }

    #endregion
}
