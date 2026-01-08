using Shopilent.Domain.Audit.Enums;
using Shopilent.Domain.Audit.Errors;
using Shopilent.Domain.Common;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Domain.Audit;

public class AuditLog : AggregateRoot
{
    private AuditLog()
    {
        // Required by EF Core
    }

    private AuditLog(
        string entityType,
        Guid entityId,
        AuditAction action,
        Guid? userId = null,
        Dictionary<string, object> oldValues = null,
        Dictionary<string, object> newValues = null,
        string ipAddress = null,
        string userAgent = null,
        string appVersion = null)
    {
        EntityType = entityType;
        EntityId = entityId;
        Action = action;
        UserId = userId;
        OldValues = oldValues;
        NewValues = newValues;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        AppVersion = appVersion;
    }

    public static Result<AuditLog> Create(
        string entityType,
        Guid entityId,
        AuditAction action,
        Guid? userId = null,
        Dictionary<string, object> oldValues = null,
        Dictionary<string, object> newValues = null,
        string ipAddress = null,
        string userAgent = null,
        string appVersion = null)
    {
        if (string.IsNullOrWhiteSpace(entityType))
            return Result.Failure<AuditLog>(AuditLogErrors.EntityTypeRequired);

        if (entityId == Guid.Empty)
            return Result.Failure<AuditLog>(AuditLogErrors.InvalidEntityId);

        return Result.Success(new AuditLog(entityType, entityId, action, userId, oldValues, newValues, ipAddress,
            userAgent, appVersion));
    }

    public static Result<AuditLog> CreateForCreate(
        string entityType,
        Guid entityId,
        Dictionary<string, object> values,
        Guid? userId = null,
        string ipAddress = null,
        string userAgent = null,
        string appVersion = null)
    {
        return Create(entityType, entityId, AuditAction.Create, userId, null, values, ipAddress, userAgent, appVersion);
    }

    public static Result<AuditLog> CreateForUpdate(
        string entityType,
        Guid entityId,
        Dictionary<string, object> oldValues,
        Dictionary<string, object> newValues,
        Guid? userId = null,
        string ipAddress = null,
        string userAgent = null,
        string appVersion = null)
    {
        return Create(entityType, entityId, AuditAction.Update, userId, oldValues, newValues, ipAddress, userAgent,
            appVersion);
    }

    public static Result<AuditLog> CreateForDelete(
        string entityType,
        Guid entityId,
        Dictionary<string, object> values,
        Guid? userId = null,
        string ipAddress = null,
        string userAgent = null,
        string appVersion = null)
    {
        return Create(entityType, entityId, AuditAction.Delete, userId, values, null, ipAddress, userAgent, appVersion);
    }

    public Guid? UserId { get; private set; }
    public string EntityType { get; private set; }
    public Guid EntityId { get; private set; }
    public AuditAction Action { get; private set; }
    public Dictionary<string, object> OldValues { get; private set; }
    public Dictionary<string, object> NewValues { get; private set; }
    public string IpAddress { get; private set; }
    public string UserAgent { get; private set; }
    public string AppVersion { get; private set; }
}
