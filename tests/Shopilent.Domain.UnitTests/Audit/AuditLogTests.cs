using Shopilent.Domain.Audit;
using Shopilent.Domain.Audit.Enums;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.ValueObjects;

namespace Shopilent.Domain.Tests.Audit;

public class AuditLogTests
{
    private Email CreateTestEmail()
    {
        return Email.Create("test@example.com").Value;
    }

    private FullName CreateTestFullName()
    {
        return FullName.Create("John", "Doe").Value;
    }

    private User CreateTestUser()
    {
        var userResult = User.Create(
            CreateTestEmail(),
            "hashed_password",
            CreateTestFullName());

        return userResult.Value;
    }

    [Fact]
    public void Create_WithValidParameters_ShouldCreateAuditLog()
    {
        // Arrange
        var entityType = "Product";
        var entityId = Guid.NewGuid();
        var action = AuditAction.Update;
        var user = CreateTestUser();
        var oldValues = new Dictionary<string, object> { { "Name", "Old Name" } };
        var newValues = new Dictionary<string, object> { { "Name", "New Name" } };
        var ipAddress = "127.0.0.1";
        var userAgent = "Test Agent";
        var appVersion = "1.0.0";

        // Act
        var result = AuditLog.Create(
            entityType,
            entityId,
            action,
            user.Id,
            oldValues,
            newValues,
            ipAddress,
            userAgent,
            appVersion);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var auditLog = result.Value;
        auditLog.EntityType.Should().Be(entityType);
        auditLog.EntityId.Should().Be(entityId);
        auditLog.Action.Should().Be(action);
        auditLog.UserId.Should().Be(user.Id);
        auditLog.OldValues.Should().BeEquivalentTo(oldValues);
        auditLog.NewValues.Should().BeEquivalentTo(newValues);
        auditLog.IpAddress.Should().Be(ipAddress);
        auditLog.UserAgent.Should().Be(userAgent);
        auditLog.AppVersion.Should().Be(appVersion);
    }

    [Fact]
    public void Create_WithEmptyEntityType_ShouldReturnFailureResult()
    {
        // Arrange
        var entityType = string.Empty;
        var entityId = Guid.NewGuid();
        var action = AuditAction.Create;

        // Act
        var result = AuditLog.Create(
            entityType,
            entityId,
            action);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AuditLog.EntityTypeRequired");
    }

    [Fact]
    public void Create_WithEmptyEntityId_ShouldReturnFailureResult()
    {
        // Arrange
        var entityType = "Product";
        var entityId = Guid.Empty;
        var action = AuditAction.Create;

        // Act
        var result = AuditLog.Create(
            entityType,
            entityId,
            action);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AuditLog.InvalidEntityId");
    }

    [Fact]
    public void CreateForCreate_ShouldCreateAuditLogWithCreateAction()
    {
        // Arrange
        var entityType = "Product";
        var entityId = Guid.NewGuid();
        var values = new Dictionary<string, object> { { "Name", "New Product" }, { "Price", 100m } };
        var user = CreateTestUser();
        var ipAddress = "127.0.0.1";
        var userAgent = "Test Agent";
        var appVersion = "1.0.0";

        // Act
        var result = AuditLog.CreateForCreate(
            entityType,
            entityId,
            values,
            user.Id,
            ipAddress,
            userAgent,
            appVersion);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var auditLog = result.Value;
        auditLog.EntityType.Should().Be(entityType);
        auditLog.EntityId.Should().Be(entityId);
        auditLog.Action.Should().Be(AuditAction.Create);
        auditLog.UserId.Should().Be(user.Id);
        auditLog.OldValues.Should().BeNull();
        auditLog.NewValues.Should().BeEquivalentTo(values);
        auditLog.IpAddress.Should().Be(ipAddress);
        auditLog.UserAgent.Should().Be(userAgent);
        auditLog.AppVersion.Should().Be(appVersion);
    }

    [Fact]
    public void CreateForUpdate_ShouldCreateAuditLogWithUpdateAction()
    {
        // Arrange
        var entityType = "Product";
        var entityId = Guid.NewGuid();
        var oldValues = new Dictionary<string, object> { { "Name", "Old Name" }, { "Price", 90m } };
        var newValues = new Dictionary<string, object> { { "Name", "New Name" }, { "Price", 100m } };
        var user = CreateTestUser();
        var ipAddress = "127.0.0.1";
        var userAgent = "Test Agent";
        var appVersion = "1.0.0";

        // Act
        var result = AuditLog.CreateForUpdate(
            entityType,
            entityId,
            oldValues,
            newValues,
            user.Id,
            ipAddress,
            userAgent,
            appVersion);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var auditLog = result.Value;
        auditLog.EntityType.Should().Be(entityType);
        auditLog.EntityId.Should().Be(entityId);
        auditLog.Action.Should().Be(AuditAction.Update);
        auditLog.UserId.Should().Be(user.Id);
        auditLog.OldValues.Should().BeEquivalentTo(oldValues);
        auditLog.NewValues.Should().BeEquivalentTo(newValues);
        auditLog.IpAddress.Should().Be(ipAddress);
        auditLog.UserAgent.Should().Be(userAgent);
        auditLog.AppVersion.Should().Be(appVersion);
    }

    [Fact]
    public void CreateForDelete_ShouldCreateAuditLogWithDeleteAction()
    {
        // Arrange
        var entityType = "Product";
        var entityId = Guid.NewGuid();
        var values = new Dictionary<string, object> { { "Name", "Deleted Product" }, { "Price", 100m } };
        var user = CreateTestUser();
        var ipAddress = "127.0.0.1";
        var userAgent = "Test Agent";
        var appVersion = "1.0.0";

        // Act
        var result = AuditLog.CreateForDelete(
            entityType,
            entityId,
            values,
            user.Id,
            ipAddress,
            userAgent,
            appVersion);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var auditLog = result.Value;
        auditLog.EntityType.Should().Be(entityType);
        auditLog.EntityId.Should().Be(entityId);
        auditLog.Action.Should().Be(AuditAction.Delete);
        auditLog.UserId.Should().Be(user.Id);
        auditLog.OldValues.Should().BeEquivalentTo(values);
        auditLog.NewValues.Should().BeNull();
        auditLog.IpAddress.Should().Be(ipAddress);
        auditLog.UserAgent.Should().Be(userAgent);
        auditLog.AppVersion.Should().Be(appVersion);
    }

    [Fact]
    public void Create_WithoutUser_ShouldHaveNullUserId()
    {
        // Arrange
        var entityType = "Product";
        var entityId = Guid.NewGuid();
        var action = AuditAction.View;

        // Act
        var result = AuditLog.Create(
            entityType,
            entityId,
            action);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var auditLog = result.Value;
        auditLog.EntityType.Should().Be(entityType);
        auditLog.EntityId.Should().Be(entityId);
        auditLog.Action.Should().Be(action);
        auditLog.UserId.Should().BeNull();
        auditLog.OldValues.Should().BeNull();
        auditLog.NewValues.Should().BeNull();
        auditLog.IpAddress.Should().BeNull();
        auditLog.UserAgent.Should().BeNull();
        auditLog.AppVersion.Should().BeNull();
    }
}
