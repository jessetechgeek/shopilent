using Microsoft.AspNetCore.Identity;
using Shopilent.Domain.Identity.Enums;

namespace Shopilent.Infrastructure.Identity.Stores;

/// <summary>
/// Custom RoleStore implementation for ASP.NET Core Identity.
/// Uses our existing UserRole enum instead of a database table.
/// No additional tables needed - roles are stored as enum values on User entity.
/// </summary>
internal sealed class CustomRoleStore : IRoleStore<IdentityRole>
{
    private static readonly List<IdentityRole> _roles = Enum.GetValues<UserRole>()
        .Select(r => new IdentityRole
        {
            Id = ((int)r).ToString(),
            Name = r.ToString(),
            NormalizedName = r.ToString().ToUpperInvariant()
        })
        .ToList();

    public Task<IdentityResult> CreateAsync(IdentityRole role, CancellationToken cancellationToken)
    {
        // Roles are predefined in UserRole enum - cannot create new ones
        return Task.FromResult(IdentityResult.Failed(new IdentityError
        {
            Code = "RoleCreationNotSupported",
            Description = "Roles are predefined and cannot be created dynamically."
        }));
    }

    public Task<IdentityResult> UpdateAsync(IdentityRole role, CancellationToken cancellationToken)
    {
        // Roles are predefined - cannot update
        return Task.FromResult(IdentityResult.Failed(new IdentityError
        {
            Code = "RoleUpdateNotSupported",
            Description = "Roles are predefined and cannot be updated."
        }));
    }

    public Task<IdentityResult> DeleteAsync(IdentityRole role, CancellationToken cancellationToken)
    {
        // Roles are predefined - cannot delete
        return Task.FromResult(IdentityResult.Failed(new IdentityError
        {
            Code = "RoleDeletionNotSupported",
            Description = "Roles are predefined and cannot be deleted."
        }));
    }

    public Task<IdentityRole?> FindByIdAsync(string roleId, CancellationToken cancellationToken)
    {
        var role = _roles.FirstOrDefault(r => r.Id == roleId);
        return Task.FromResult(role);
    }

    public Task<IdentityRole?> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
    {
        var role = _roles.FirstOrDefault(r => r.NormalizedName == normalizedRoleName);
        return Task.FromResult(role);
    }

    public Task<string?> GetNormalizedRoleNameAsync(IdentityRole role, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(role.NormalizedName);
    }

    public Task<string> GetRoleIdAsync(IdentityRole role, CancellationToken cancellationToken)
    {
        return Task.FromResult(role.Id);
    }

    public Task<string?> GetRoleNameAsync(IdentityRole role, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(role.Name);
    }

    public Task SetNormalizedRoleNameAsync(IdentityRole role, string? normalizedName, CancellationToken cancellationToken)
    {
        role.NormalizedName = normalizedName;
        return Task.CompletedTask;
    }

    public Task SetRoleNameAsync(IdentityRole role, string? roleName, CancellationToken cancellationToken)
    {
        role.Name = roleName;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // Nothing to dispose
    }
}
