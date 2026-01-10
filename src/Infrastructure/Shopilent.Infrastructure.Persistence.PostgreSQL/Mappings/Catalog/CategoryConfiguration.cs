using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shopilent.Domain.Catalog;

namespace Shopilent.Infrastructure.Persistence.PostgreSQL.Mappings.Catalog;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");

        builder.HasKey(c => c.Id);

        // Base entity properties
        builder.Property(c => c.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // Auditable entity properties
        builder.Property(c => c.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("uuid");

        builder.Property(c => c.ModifiedBy)
            .HasColumnName("modified_by")
            .HasColumnType("uuid");

        builder.Property(c => c.LastModified)
            .HasColumnName("last_modified")
            .HasColumnType("timestamp with time zone");

        // Category specific properties
        builder.Property(c => c.Name)
            .HasColumnName("name")
            .HasColumnType("varchar(100)")
            .IsRequired();

        builder.Property(c => c.Description)
            .HasColumnName("description")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(c => c.ParentId)
            .HasColumnName("parent_id")
            .HasColumnType("uuid")
            .IsRequired(false);

        builder.Property(c => c.Level)
            .HasColumnName("level")
            .HasColumnType("integer")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(c => c.Path)
            .HasColumnName("path")
            .HasColumnType("text");

        builder.Property(c => c.IsActive)
            .HasColumnName("is_active")
            .HasColumnType("boolean")
            .HasDefaultValue(true)
            .IsRequired();

        // Value object mappings
        builder.OwnsOne(c => c.Slug, slug =>
        {
            slug.Property(s => s.Value)
                .HasColumnName("slug")
                .HasColumnType("varchar(255)")
                .IsRequired();

            slug.HasIndex(s => s.Value)
                .IsUnique();
        });

        // Relationships
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(c => c.ParentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(c => c.ParentId);
        builder.HasIndex(c => c.Path);
        builder.HasIndex(c => c.IsActive);

        // Optimistic Concurrency
        builder.Property(c => c.Version)
            .HasColumnName("version")
            .HasDefaultValue(0)
            .IsConcurrencyToken();
    }
}
