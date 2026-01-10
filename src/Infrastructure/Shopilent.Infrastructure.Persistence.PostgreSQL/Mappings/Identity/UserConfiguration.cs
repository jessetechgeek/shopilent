using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shopilent.Domain.Identity;
using System.Reflection;

namespace Shopilent.Infrastructure.Persistence.PostgreSQL.Mappings.Identity;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        // Base entity properties
        builder.Property(u => u.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(u => u.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(u => u.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // Auditable entity properties
        builder.Property(u => u.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("uuid");

        builder.Property(u => u.ModifiedBy)
            .HasColumnName("modified_by")
            .HasColumnType("uuid");

        builder.Property(u => u.LastModified)
            .HasColumnName("last_modified")
            .HasColumnType("timestamp with time zone");

        // User specific properties
        builder.Property(u => u.PasswordHash)
            .HasColumnName("password_hash")
            .HasColumnType("varchar(255)")
            .IsRequired();

        builder.Property(u => u.IsActive)
            .HasColumnName("is_active")
            .HasColumnType("boolean")
            .IsRequired();

        builder.Property(u => u.Role)
            .HasColumnName("role")
            .HasColumnType("varchar(50)")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(u => u.LastLogin)
            .HasColumnName("last_login")
            .HasColumnType("timestamp with time zone");

        builder.Property(u => u.EmailVerified)
            .HasColumnName("email_verified")
            .HasColumnType("boolean")
            .IsRequired();

        builder.Property(u => u.EmailVerificationToken)
            .HasColumnName("email_verification_token")
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        builder.Property(u => u.EmailVerificationExpires)
            .HasColumnName("email_verification_expires")
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.Property(u => u.PasswordResetToken)
            .HasColumnName("password_reset_token")
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        builder.Property(u => u.PasswordResetExpires)
            .HasColumnName("password_reset_expires")
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.Property(u => u.FailedLoginAttempts)
            .HasColumnName("failed_login_attempts")
            .HasColumnType("integer")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(u => u.LastFailedAttempt)
            .HasColumnName("last_failed_attempt")
            .HasColumnType("timestamp with time zone");

        // Value object mappings
        builder.OwnsOne(u => u.Email, email =>
        {
            email.Property(e => e.Value)
                .HasColumnName("email")
                .HasColumnType("varchar(255)")
                .IsRequired();

            email.HasIndex(e => e.Value)
                .IsUnique();
        });

        builder.OwnsOne(u => u.FullName, fullName =>
        {
            fullName.Property(fn => fn.FirstName)
                .HasColumnName("first_name")
                .HasColumnType("varchar(100)")
                .IsRequired();

            fullName.Property(fn => fn.LastName)
                .HasColumnName("last_name")
                .HasColumnType("varchar(100)")
                .IsRequired();

            fullName.Property(fn => fn.MiddleName)
                .HasColumnName("middle_name")
                .HasColumnType("varchar(100)");
        });

        builder.OwnsOne(u => u.Phone, phone =>
        {
            phone.Property(p => p.Value)
                .HasColumnName("phone")
                .HasColumnType("varchar(50)")
                .IsRequired(false);
        });

        // Relationships
        builder.HasMany(u => u.RefreshTokens)
            .WithOne()
            .HasForeignKey("UserId")
            .OnDelete(DeleteBehavior.Cascade);

        // Optimistic Concurrency
        builder.Property(u => u.Version)
            .HasColumnName("version")
            .HasDefaultValue(0)
            .IsConcurrencyToken();

        // Check constraint for email format
        builder.HasCheckConstraint("check_email_format",
            "email ~* '^[A-Za-z0-9._%-]+@[A-Za-z0-9.-]+[.][A-Za-z]+$'");
    }
}