using System.Reflection;
using Bogus;
using Shopilent.Domain.Audit;
using Shopilent.Domain.Audit.Enums;
using Shopilent.Domain.Identity;

namespace Shopilent.Infrastructure.IntegrationTests.TestData.Builders;

public class AuditLogBuilder
{
    private readonly Faker _faker = new();
    private readonly string _entityType;
    private readonly Guid _entityId;
    private readonly AuditAction _action;
    private User _user;
    private Dictionary<string, object> _oldValues;
    private Dictionary<string, object> _newValues;
    private string _ipAddress;
    private string _userAgent;
    private string _appVersion;
    private DateTime? _createdAt;

    public AuditLogBuilder(string entityType = null, Guid? entityId = null, AuditAction? action = null)
    {
        _entityType = entityType ?? _faker.PickRandom("User", "Product", "Category", "Order");
        _entityId = entityId ?? _faker.Random.Guid();
        _action = action ?? _faker.PickRandom<AuditAction>();

        // Set reasonable defaults
        _ipAddress = _faker.Internet.Ip();
        _userAgent = _faker.Internet.UserAgent();
        _appVersion = _faker.System.Version().ToString();
    }

    public AuditLogBuilder WithUser(User user)
    {
        _user = user;
        return this;
    }

    public AuditLogBuilder WithOldValues(Dictionary<string, object> oldValues)
    {
        _oldValues = oldValues;
        return this;
    }

    public AuditLogBuilder WithNewValues(Dictionary<string, object> newValues)
    {
        _newValues = newValues;
        return this;
    }

    public AuditLogBuilder WithIpAddress(string ipAddress)
    {
        _ipAddress = ipAddress;
        return this;
    }

    public AuditLogBuilder WithUserAgent(string userAgent)
    {
        _userAgent = userAgent;
        return this;
    }

    public AuditLogBuilder WithAppVersion(string appVersion)
    {
        _appVersion = appVersion;
        return this;
    }

    public AuditLogBuilder WithCreatedAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public AuditLogBuilder ForCreateAction()
    {
        _newValues = new Dictionary<string, object>
        {
            ["Name"] = _faker.Lorem.Word(),
            ["CreatedAt"] = DateTime.UtcNow,
            ["Status"] = "Active"
        };
        return this;
    }

    public AuditLogBuilder ForUpdateAction()
    {
        _oldValues = new Dictionary<string, object>
        {
            ["Name"] = _faker.Lorem.Word(),
            ["UpdatedAt"] = DateTime.UtcNow.AddDays(-1),
            ["Status"] = "Active"
        };

        _newValues = new Dictionary<string, object>
        {
            ["Name"] = _faker.Lorem.Word(),
            ["UpdatedAt"] = DateTime.UtcNow,
            ["Status"] = "Modified"
        };
        return this;
    }

    public AuditLogBuilder ForDeleteAction()
    {
        _oldValues = new Dictionary<string, object>
        {
            ["Name"] = _faker.Lorem.Word(),
            ["DeletedAt"] = DateTime.UtcNow,
            ["Status"] = "Deleted"
        };
        return this;
    }

    public AuditLog Build()
    {
        var result = AuditLog.Create(
            _entityType,
            _entityId,
            _action,
            _user?.Id,
            _oldValues,
            _newValues,
            _ipAddress,
            _userAgent,
            _appVersion
        );

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Failed to create AuditLog: {result.Error.Message}");
        }

        var auditLog = result.Value;

        // Override CreatedAt if specified
        if (_createdAt.HasValue)
        {
            var createdAtField = typeof(AuditLog).BaseType.BaseType.BaseType
                .GetField("<CreatedAt>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            createdAtField?.SetValue(auditLog, _createdAt.Value);
        }

        return auditLog;
    }

    public static AuditLog CreateDefaultAuditLog()
    {
        return new AuditLogBuilder().Build();
    }

    public static AuditLog CreateForEntity(string entityType, Guid entityId, AuditAction action)
    {
        return new AuditLogBuilder(entityType, entityId, action).Build();
    }

    public static AuditLog CreateForUser(User user, string entityType = null)
    {
        return new AuditLogBuilder(entityType)
            .WithUser(user)
            .Build();
    }

    public static AuditLog CreateCreateAuditLog(string entityType, Guid entityId, User user = null)
    {
        return new AuditLogBuilder(entityType, entityId, AuditAction.Create)
            .WithUser(user)
            .ForCreateAction()
            .Build();
    }

    public static AuditLog CreateUpdateAuditLog(string entityType, Guid entityId, User user = null)
    {
        return new AuditLogBuilder(entityType, entityId, AuditAction.Update)
            .WithUser(user)
            .ForUpdateAction()
            .Build();
    }

    public static AuditLog CreateDeleteAuditLog(string entityType, Guid entityId, User user = null)
    {
        return new AuditLogBuilder(entityType, entityId, AuditAction.Delete)
            .WithUser(user)
            .ForDeleteAction()
            .Build();
    }

    public static List<AuditLog> CreateMultipleWithDistinctTimestamps(int count, User user = null, DateTime? baseTime = null)
    {
        var auditLogs = new List<AuditLog>();
        var startTime = baseTime ?? DateTime.UtcNow.AddMinutes(-count);

        for (int i = 0; i < count; i++)
        {
            var timestamp = startTime.AddSeconds(i * 2); // 2 seconds apart to ensure distinct ordering
            var auditLog = new AuditLogBuilder()
                .WithUser(user)
                .WithCreatedAt(timestamp)
                .Build();
            auditLogs.Add(auditLog);
        }

        return auditLogs;
    }

}
