using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Domain.Audit;
using Shopilent.Domain.Audit.Enums;
using Shopilent.Domain.Common;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Outbox;

namespace Shopilent.Infrastructure.Persistence.PostgreSQL.Interceptors;

public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserContext _currentUserService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditSaveChangesInterceptor(
        ICurrentUserContext currentUserService,
        IHttpContextAccessor httpContextAccessor)
    {
        _currentUserService = currentUserService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        UpdateEntities(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateEntities(eventData.Context);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateEntities(DbContext context)
    {
        if (context == null) return;

        // Safely access HTTP context - might be null in some scenarios
        string ipAddress = null;
        string userAgent = null;

        if (_httpContextAccessor?.HttpContext != null)
        {
            ipAddress = _httpContextAccessor.HttpContext.Connection.RemoteIpAddress?.ToString();
            userAgent = _httpContextAccessor.HttpContext.Request.Headers["User-Agent"].ToString();
        }

        // Safely get user ID - might be null during login/authentication
        Guid? userId = null;
        User user = null;

        try
        {
            userId = _currentUserService?.UserId;

            // If user ID is available, try to get the user entity
            if (userId.HasValue)
            {
                user = context.Set<User>().Find(userId.Value);
            }
        }
        catch (Exception)
        {
            // Silently continue without user context if it can't be obtained
            // This handles cases like authentication where user context isn't established yet
        }

        var entries = context.ChangeTracker.Entries<Entity>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in entries)
        {
            // Skip AuditLog entities to prevent recursive auditing
            if (entry.Entity is AuditLog) continue;
            if (entry.Entity is OutboxMessage) continue;

            // Skip RefreshToken entities during authentication to prevent errors
            if (entry.Entity is RefreshToken && userId == null) continue;

            var entityType = entry.Entity.GetType().Name;

            // Skip auditing for sensitive operations when there's no authenticated user
            if (userId == null && (entityType == nameof(User) || entityType == nameof(RefreshToken)))
            {
                continue;
            }

            // Create appropriate AuditLog based on the state
            switch (entry.State)
            {
                case EntityState.Added:
                    var newValues = GetEntityValues(entry);
                    CreateAuditLog(context, entityType, entry.Entity.Id, AuditAction.Create, user,
                        new Dictionary<string, object>(), newValues, ipAddress, userAgent);
                    break;

                case EntityState.Modified:
                    entry.Entity.IncrementVersion();

                    var oldValues = GetOriginalValues(entry);
                    var currentValues = GetEntityValues(entry);
                    CreateAuditLog(context, entityType, entry.Entity.Id, AuditAction.Update, user,
                        oldValues, currentValues, ipAddress, userAgent);
                    break;

                case EntityState.Deleted:
                    var deletedValues = GetEntityValues(entry);
                    CreateAuditLog(context, entityType, entry.Entity.Id, AuditAction.Delete, user,
                        deletedValues, new Dictionary<string, object>(), ipAddress, userAgent);
                    break;
            }

            // For AuditableEntity, update audit information
            if (entry.Entity is AuditableEntity auditableEntity &&
                entry.State is EntityState.Added or EntityState.Modified)
            {
                if (entry.State == EntityState.Added)
                {
                    auditableEntity.SetCreationAuditInfo(userId);
                }
                else
                {
                    auditableEntity.SetAuditInfo(userId);
                }
            }
        }
    }

    private static Dictionary<string, object> GetEntityValues(EntityEntry entry)
    {
        var values = entry.Properties
            .Where(p => !p.Metadata.IsShadowProperty())
            .ToDictionary(
                p => p.Metadata.Name,
                p => SerializePropertyValue(p.CurrentValue));

        // Ensure the dictionary is never empty (to satisfy NOT NULL constraints)
        if (!values.Any())
        {
            values.Add("_placeholder", "empty");
        }

        return values;
    }

    private static Dictionary<string, object> GetOriginalValues(EntityEntry entry)
    {
        var values = entry.Properties
            .Where(p => !p.Metadata.IsShadowProperty())
            .ToDictionary(
                p => p.Metadata.Name,
                p => SerializePropertyValue(p.OriginalValue));

        // Ensure the dictionary is never empty (to satisfy NOT NULL constraints)
        if (!values.Any())
        {
            values.Add("_placeholder", "empty");
        }

        return values;
    }

    private static object SerializePropertyValue(object value)
    {
        if (value == null)
            return null;

        // Handle special types that might need custom serialization
        return value switch
        {
            ValueObject => JsonSerializer.Serialize(value),
            DateTime dateTime => dateTime.ToString("o"), // ISO 8601 format
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("o"),
            IEnumerable<object> enumerable => JsonSerializer.Serialize(enumerable),
            _ => value
        };
    }

    private static void CreateAuditLog(
        DbContext context,
        string entityType,
        Guid entityId,
        AuditAction action,
        User user,
        Dictionary<string, object> oldValues,
        Dictionary<string, object> newValues,
        string ipAddress,
        string userAgent)
    {
        try
        {
            // Ensure we pass non-null dictionaries
            oldValues ??= new Dictionary<string, object>();
            newValues ??= new Dictionary<string, object>();

            // Ensure dictionaries are not empty to satisfy database constraints
            if (!oldValues.Any()) oldValues.Add("_placeholder", "empty");
            if (!newValues.Any()) newValues.Add("_placeholder", "empty");

            var userId = user?.Id;

            var auditLogResult = AuditLog.Create(
                entityType,
                entityId,
                action,
                userId,
                oldValues,
                newValues,
                ipAddress,
                userAgent);

            if (auditLogResult.IsSuccess)
            {
                context.Set<AuditLog>().Add(auditLogResult.Value);
            }
            else
            {
                // Log error if available in a production environment
                // Console.WriteLine($"Failed to create audit log: {auditLogResult.Error?.Message}");
            }
        }
        catch (Exception ex)
        {
            // If audit logging fails, we don't want to fail the entire operation
            // Log this issue to a proper logging system in a production environment
            // Console.WriteLine($"Exception in audit logging: {ex.Message}");
        }
    }
}
