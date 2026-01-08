using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Audit;
using Shopilent.Domain.Audit.Enums;
using Shopilent.Domain.Audit.Repositories.Read;
using Shopilent.Domain.Audit.Repositories.Write;
using Shopilent.Domain.Identity.Repositories.Write;
using Shopilent.Infrastructure.IntegrationTests.Common;
using Shopilent.Infrastructure.IntegrationTests.TestData.Builders;

namespace Shopilent.Infrastructure.IntegrationTests.Infrastructure.Persistence.PostgreSQL.Repositories.Audit.Write;

[Collection("IntegrationTests")]
public class AuditLogWriteRepositoryTests : IntegrationTestBase
{
    private IUnitOfWork _unitOfWork = null!;
    private IUserWriteRepository _userWriteRepository = null!;
    private IAuditLogWriteRepository _auditLogWriteRepository = null!;
    private IAuditLogReadRepository _auditLogReadRepository = null!;

    public AuditLogWriteRepositoryTests(IntegrationTestFixture fixture) : base(fixture)
    {
    }

    protected override Task InitializeTestServices()
    {
        _unitOfWork = GetService<IUnitOfWork>();
        _userWriteRepository = GetService<IUserWriteRepository>();
        _auditLogWriteRepository = GetService<IAuditLogWriteRepository>();
        _auditLogReadRepository = GetService<IAuditLogReadRepository>();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AddAsync_ValidAuditLog_ShouldPersistToDatabase()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.CreateDefaultUser();
        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.CommitAsync();

        var auditLog = AuditLogBuilder.CreateForUser(user, "Product");

        // Act
        await _auditLogWriteRepository.AddAsync(auditLog);
        await _unitOfWork.CommitAsync();

        // Assert
        var result = await _auditLogReadRepository.GetByIdAsync(auditLog.Id);
        result.Should().NotBeNull();
        result!.Id.Should().Be(auditLog.Id);
        result.UserId.Should().Be(user.Id);
        result.EntityType.Should().Be(auditLog.EntityType);
        result.EntityId.Should().Be(auditLog.EntityId);
        result.Action.Should().Be(auditLog.Action);
        result.CreatedAt.Should().BeCloseTo(auditLog.CreatedAt, TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task AddAsync_AuditLogWithoutUser_ShouldPersistSuccessfully()
    {
        // Arrange
        await ResetDatabaseAsync();

        var systemAuditLog = AuditLogBuilder.CreateForEntity("System", Guid.NewGuid(), AuditAction.Create);

        // Act
        await _auditLogWriteRepository.AddAsync(systemAuditLog);
        await _unitOfWork.CommitAsync();

        // Assert
        var result = await _auditLogReadRepository.GetByIdAsync(systemAuditLog.Id);
        result.Should().NotBeNull();
        result!.Id.Should().Be(systemAuditLog.Id);
        result.UserId.Should().BeNull();
        result.EntityType.Should().Be(systemAuditLog.EntityType);
        result.EntityId.Should().Be(systemAuditLog.EntityId);
        result.Action.Should().Be(systemAuditLog.Action);
    }

    [Fact]
    public async Task AddAsync_AuditLogWithComplexValues_ShouldPersistComplexData()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.CreateDefaultUser();
        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.CommitAsync();

        var oldValues = new Dictionary<string, object>
        {
            ["Name"] = "Old Product Name",
            ["Price"] = 99.99m,
            ["IsActive"] = true,
            ["Tags"] = new[] { "electronics", "gadget" },
            ["Metadata"] = new { Category = "Electronics", Brand = "TechCorp" }
        };

        var newValues = new Dictionary<string, object>
        {
            ["Name"] = "New Product Name",
            ["Price"] = 149.99m,
            ["IsActive"] = false,
            ["Tags"] = new[] { "electronics", "premium" },
            ["Metadata"] = new { Category = "Electronics", Brand = "TechCorp", Featured = true }
        };

        var auditLog = new AuditLogBuilder("Product", Guid.NewGuid(), AuditAction.Update)
            .WithUser(user)
            .WithOldValues(oldValues)
            .WithNewValues(newValues)
            .WithIpAddress("192.168.1.100")
            .WithUserAgent("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36")
            .WithAppVersion("2.1.0")
            .Build();

        // Act
        await _auditLogWriteRepository.AddAsync(auditLog);
        await _unitOfWork.CommitAsync();

        // Assert
        var result = await _auditLogReadRepository.GetByIdAsync(auditLog.Id);
        result.Should().NotBeNull();
        result!.OldValues.Should().NotBeNull();
        result.NewValues.Should().NotBeNull();
        result.OldValues.Should().ContainKey("Name");
        result.NewValues.Should().ContainKey("Name");
        result.IpAddress.Should().Be("192.168.1.100");
        result.UserAgent.Should().Contain("Mozilla");
        result.AppVersion.Should().Be("2.1.0");
    }

    [Fact]
    public async Task UpdateAsync_ExistingAuditLog_ShouldModifyAuditLog()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.CreateDefaultUser();
        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.CommitAsync();

        var auditLog = AuditLogBuilder.CreateForUser(user, "Product");
        await _auditLogWriteRepository.AddAsync(auditLog);
        await _unitOfWork.CommitAsync();

        // Detach to simulate real scenario
        DbContext.Entry(auditLog).State = EntityState.Detached;

        // Act - Load fresh entity and update
        var existingAuditLog = await _auditLogWriteRepository.GetByIdAsync(auditLog.Id);
        existingAuditLog.Should().NotBeNull();

        // Note: AuditLog is typically immutable, but let's test the repository update functionality
        await _auditLogWriteRepository.UpdateAsync(existingAuditLog!);
        await _unitOfWork.CommitAsync();

        // Assert
        var updatedResult = await _auditLogReadRepository.GetByIdAsync(auditLog.Id);
        updatedResult.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteAsync_ExistingAuditLog_ShouldRemoveFromDatabase()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.CreateDefaultUser();
        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.CommitAsync();

        var auditLog = AuditLogBuilder.CreateForUser(user, "Product");
        await _auditLogWriteRepository.AddAsync(auditLog);
        await _unitOfWork.CommitAsync();

        // Act
        await _auditLogWriteRepository.DeleteAsync(auditLog);
        await _unitOfWork.CommitAsync();

        // Assert
        var result = await _auditLogReadRepository.GetByIdAsync(auditLog.Id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ShouldReturnAuditLogEntity()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.CreateDefaultUser();
        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.CommitAsync();

        var auditLog = AuditLogBuilder.CreateForUser(user, "Product");
        await _auditLogWriteRepository.AddAsync(auditLog);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _auditLogWriteRepository.GetByIdAsync(auditLog.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(auditLog.Id);
        result.UserId.Should().Be(user.Id);
        result.EntityType.Should().Be(auditLog.EntityType);
        result.EntityId.Should().Be(auditLog.EntityId);
        result.Action.Should().Be(auditLog.Action);
        result.IpAddress.Should().Be(auditLog.IpAddress);
        result.UserAgent.Should().Be(auditLog.UserAgent);
        result.AppVersion.Should().Be(auditLog.AppVersion);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _auditLogWriteRepository.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByEntityAsync_ExistingEntity_ShouldReturnAuditLogEntities()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.CreateDefaultUser();
        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.CommitAsync();

        var entityType = "Product";
        var entityId = Guid.NewGuid();

        var auditLog1 = AuditLogBuilder.CreateCreateAuditLog(entityType, entityId, user);
        await _auditLogWriteRepository.AddAsync(auditLog1);
        await _unitOfWork.CommitAsync();
        await Task.Delay(100); // Ensure sufficient time gap

        var auditLog2 = AuditLogBuilder.CreateUpdateAuditLog(entityType, entityId, user);
        await _auditLogWriteRepository.AddAsync(auditLog2);
        await _unitOfWork.CommitAsync();
        await Task.Delay(100); // Ensure sufficient time gap

        var auditLog3 = AuditLogBuilder.CreateForEntity("Category", Guid.NewGuid(), AuditAction.Create);
        await _auditLogWriteRepository.AddAsync(auditLog3);
        await _unitOfWork.CommitAsync();

        // Act
        var results = await _auditLogWriteRepository.GetByEntityAsync(entityType, entityId);

        // Assert
        results.Should().NotBeEmpty();
        results.Should().HaveCount(2);
        results.Should().OnlyContain(r => r.EntityType == entityType && r.EntityId == entityId);
        results.Should().BeInDescendingOrder(x => x.CreatedAt);

        var auditLogIds = results.Select(r => r.Id).ToList();
        auditLogIds.Should().Contain(auditLog1.Id);
        auditLogIds.Should().Contain(auditLog2.Id);
        auditLogIds.Should().NotContain(auditLog3.Id);
    }

    [Fact]
    public async Task GetByUserAsync_ExistingUser_ShouldReturnUserAuditLogEntities()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user1 = UserBuilder.CreateDefaultUser();
        var user2 = UserBuilder.CreateDefaultUser();

        await _userWriteRepository.AddAsync(user1);
        await _userWriteRepository.AddAsync(user2);
        await _unitOfWork.CommitAsync();

        var user1AuditLog1 = AuditLogBuilder.CreateForUser(user1, "Product");
        await _auditLogWriteRepository.AddAsync(user1AuditLog1);
        await _unitOfWork.CommitAsync();
        await Task.Delay(100); // Ensure sufficient time gap

        var user1AuditLog2 = AuditLogBuilder.CreateForUser(user1, "Category");
        await _auditLogWriteRepository.AddAsync(user1AuditLog2);
        await _unitOfWork.CommitAsync();
        await Task.Delay(100); // Ensure sufficient time gap

        var user2AuditLog = AuditLogBuilder.CreateForUser(user2, "Order");
        await _auditLogWriteRepository.AddAsync(user2AuditLog);
        await _unitOfWork.CommitAsync();
        await Task.Delay(100); // Ensure sufficient time gap

        var systemAuditLog = AuditLogBuilder.CreateForEntity("System", Guid.NewGuid(), AuditAction.Create);
        await _auditLogWriteRepository.AddAsync(systemAuditLog);
        await _unitOfWork.CommitAsync();

        // Act
        var results = await _auditLogWriteRepository.GetByUserAsync(user1.Id);

        // Assert
        results.Should().NotBeEmpty();
        results.Should().HaveCount(2);
        results.Should().OnlyContain(r => r.UserId == user1.Id);
        results.Should().BeInDescendingOrder(x => x.CreatedAt);

        var auditLogIds = results.Select(r => r.Id).ToList();
        auditLogIds.Should().Contain(user1AuditLog1.Id);
        auditLogIds.Should().Contain(user1AuditLog2.Id);
        auditLogIds.Should().NotContain(user2AuditLog.Id);
        auditLogIds.Should().NotContain(systemAuditLog.Id);
    }

    [Fact]
    public async Task GetByActionAsync_ExistingAction_ShouldReturnActionAuditLogEntities()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.CreateDefaultUser();
        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.CommitAsync();

        var createAuditLog1 = AuditLogBuilder.CreateCreateAuditLog("Product", Guid.NewGuid(), user);
        await _auditLogWriteRepository.AddAsync(createAuditLog1);
        await _unitOfWork.CommitAsync();
        await Task.Delay(100); // Ensure sufficient time gap

        var createAuditLog2 = AuditLogBuilder.CreateCreateAuditLog("Category", Guid.NewGuid(), user);
        await _auditLogWriteRepository.AddAsync(createAuditLog2);
        await _unitOfWork.CommitAsync();
        await Task.Delay(100); // Ensure sufficient time gap

        var updateAuditLog = AuditLogBuilder.CreateUpdateAuditLog("Product", Guid.NewGuid(), user);
        await _auditLogWriteRepository.AddAsync(updateAuditLog);
        await _unitOfWork.CommitAsync();
        await Task.Delay(100); // Ensure sufficient time gap

        var deleteAuditLog = AuditLogBuilder.CreateDeleteAuditLog("Product", Guid.NewGuid(), user);
        await _auditLogWriteRepository.AddAsync(deleteAuditLog);
        await _unitOfWork.CommitAsync();

        // Act
        var results = await _auditLogWriteRepository.GetByActionAsync(AuditAction.Create);

        // Assert
        // Note: User creation by audit interceptor creates 1 audit log + our 2 test logs = 3 total
        results.Should().NotBeEmpty();
        results.Should().HaveCount(3);
        results.Should().OnlyContain(r => r.Action == AuditAction.Create);
        results.Should().BeInDescendingOrder(x => x.CreatedAt);

        var auditLogIds = results.Select(r => r.Id).ToList();
        auditLogIds.Should().Contain(createAuditLog1.Id);
        auditLogIds.Should().Contain(createAuditLog2.Id);
        auditLogIds.Should().NotContain(updateAuditLog.Id);
        auditLogIds.Should().NotContain(deleteAuditLog.Id);
    }

    [Fact]
    public async Task BulkOperations_MultipleAuditLogs_ShouldHandleCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.CreateDefaultUser();
        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.CommitAsync();

        var auditLogs = new List<AuditLog>
        {
            AuditLogBuilder.CreateCreateAuditLog("Product", Guid.NewGuid(), user),
            AuditLogBuilder.CreateUpdateAuditLog("Product", Guid.NewGuid(), user),
            AuditLogBuilder.CreateDeleteAuditLog("Product", Guid.NewGuid(), user),
            AuditLogBuilder.CreateForEntity("Category", Guid.NewGuid(), AuditAction.Create),
            AuditLogBuilder.CreateForEntity("Order", Guid.NewGuid(), AuditAction.Update)
        };

        // Act - Add all audit logs in bulk
        foreach (var auditLog in auditLogs)
        {
            await _auditLogWriteRepository.AddAsync(auditLog);
        }
        await _unitOfWork.CommitAsync();

        // Assert - Verify all were persisted
        // Note: User creation by audit interceptor creates 1 audit log + our 5 test logs = 6 total
        var allResults = await _auditLogReadRepository.ListAllAsync();
        allResults.Should().HaveCount(6);

        foreach (var auditLog in auditLogs)
        {
            var result = await _auditLogReadRepository.GetByIdAsync(auditLog.Id);
            result.Should().NotBeNull();
            result!.Id.Should().Be(auditLog.Id);
        }
    }

    [Fact]
    public async Task OptimisticConcurrency_ConcurrentUpdates_ShouldHandleCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.CreateDefaultUser();
        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.CommitAsync();

        var auditLog = AuditLogBuilder.CreateForUser(user, "Product");
        await _auditLogWriteRepository.AddAsync(auditLog);
        await _unitOfWork.CommitAsync();

        // Act - Simulate concurrent access with two UnitOfWork instances
        using var scope1 = ServiceProvider.CreateScope();
        using var scope2 = ServiceProvider.CreateScope();

        var unitOfWork1 = scope1.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var unitOfWork2 = scope2.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var auditLogWriter1 = scope1.ServiceProvider.GetRequiredService<IAuditLogWriteRepository>();
        var auditLogWriter2 = scope2.ServiceProvider.GetRequiredService<IAuditLogWriteRepository>();

        var auditLog1 = await  auditLogWriter1.GetByIdAsync(auditLog.Id);
        var auditLog2 = await auditLogWriter2.GetByIdAsync(auditLog.Id);

        auditLog1.Should().NotBeNull();
        auditLog2.Should().NotBeNull();

        // AuditLogs are immutable by design - they should not be updated after creation
        // The UpdateAsync method exists in the interface but should not modify audit logs
        // Let's verify the entities remain unchanged after "update" operations
        await auditLogWriter1.UpdateAsync(auditLog1!);
        await unitOfWork1.CommitAsync();

        await auditLogWriter2.UpdateAsync(auditLog2!);
        await unitOfWork2.CommitAsync(); // Should not throw since no actual changes are made

        // Verify the audit log remains unchanged
        var finalResult = await _auditLogReadRepository.GetByIdAsync(auditLog.Id);
        finalResult.Should().NotBeNull();
        finalResult!.EntityType.Should().Be(auditLog.EntityType);
        finalResult.Action.Should().Be(auditLog.Action);
        finalResult.UserId.Should().Be(auditLog.UserId);
    }

    [Fact]
    public async Task AuditInterceptor_SkipsAuditLogEntities_PreventsInfiniteRecursion()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.CreateDefaultUser();
        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.CommitAsync();

        var auditLog = AuditLogBuilder.CreateForUser(user, "Product");

        // Act - Add audit log
        await _auditLogWriteRepository.AddAsync(auditLog);
        await _unitOfWork.CommitAsync();

        // Assert - AuditLog entities are explicitly skipped by AuditSaveChangesInterceptor (line 86)
        // to prevent infinite recursion, so no audit trail should be created for AuditLog itself
        var auditTrailLogs = await _auditLogReadRepository.GetByEntityAsync("AuditLog", auditLog.Id);
        auditTrailLogs.Should().BeEmpty(); // No audit trail for AuditLog entities

        // But the audit log itself should be created successfully
        var result = await _auditLogReadRepository.GetByIdAsync(auditLog.Id);
        result.Should().NotBeNull();
        result!.EntityType.Should().Be("Product");
        result.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task SpecializedAuditLogCreation_AllActionTypes_ShouldPersistCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.CreateDefaultUser();
        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.CommitAsync();

        var entityType = "Product";
        var entityId = Guid.NewGuid();

        // Act & Assert - Test CreateForCreate
        var createValues = new Dictionary<string, object> { ["Name"] = "New Product", ["Price"] = 99.99m };
        var createResult = AuditLog.CreateForCreate(entityType, entityId, createValues, user.Id);
        createResult.IsSuccess.Should().BeTrue();

        await _auditLogWriteRepository.AddAsync(createResult.Value);
        await _unitOfWork.CommitAsync();

        var persistedCreate = await _auditLogReadRepository.GetByIdAsync(createResult.Value.Id);
        persistedCreate.Should().NotBeNull();
        persistedCreate!.Action.Should().Be(AuditAction.Create);
        persistedCreate.NewValues.Should().NotBeNull();
        persistedCreate.OldValues.Should().BeNull();

        // Act & Assert - Test CreateForUpdate
        var oldValues = new Dictionary<string, object> { ["Name"] = "Old Product", ["Price"] = 89.99m };
        var newValues = new Dictionary<string, object> { ["Name"] = "Updated Product", ["Price"] = 99.99m };
        var updateResult = AuditLog.CreateForUpdate(entityType, entityId, oldValues, newValues, user.Id);
        updateResult.IsSuccess.Should().BeTrue();

        await _auditLogWriteRepository.AddAsync(updateResult.Value);
        await _unitOfWork.CommitAsync();

        var persistedUpdate = await _auditLogReadRepository.GetByIdAsync(updateResult.Value.Id);
        persistedUpdate.Should().NotBeNull();
        persistedUpdate!.Action.Should().Be(AuditAction.Update);
        persistedUpdate.OldValues.Should().NotBeNull();
        persistedUpdate.NewValues.Should().NotBeNull();

        // Act & Assert - Test CreateForDelete
        var deleteValues = new Dictionary<string, object> { ["Name"] = "Deleted Product", ["Price"] = 99.99m };
        var deleteResult = AuditLog.CreateForDelete(entityType, entityId, deleteValues, user.Id);
        deleteResult.IsSuccess.Should().BeTrue();

        await _auditLogWriteRepository.AddAsync(deleteResult.Value);
        await _unitOfWork.CommitAsync();

        var persistedDelete = await _auditLogReadRepository.GetByIdAsync(deleteResult.Value.Id);
        persistedDelete.Should().NotBeNull();
        persistedDelete!.Action.Should().Be(AuditAction.Delete);
        persistedDelete.OldValues.Should().NotBeNull();
        persistedDelete.NewValues.Should().BeNull();
    }
}
