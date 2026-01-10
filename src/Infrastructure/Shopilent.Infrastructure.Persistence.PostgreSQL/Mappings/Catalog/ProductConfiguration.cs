using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shopilent.Domain.Catalog;

namespace Shopilent.Infrastructure.Persistence.PostgreSQL.Mappings.Catalog;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");

        builder.HasKey(p => p.Id);

        // Base entity properties
        builder.Property(p => p.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // Auditable entity properties
        builder.Property(p => p.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("uuid");

        builder.Property(p => p.ModifiedBy)
            .HasColumnName("modified_by")
            .HasColumnType("uuid");

        builder.Property(p => p.LastModified)
            .HasColumnName("last_modified")
            .HasColumnType("timestamp with time zone");

        // Product specific properties
        builder.Property(p => p.Name)
            .HasColumnName("name")
            .HasColumnType("varchar(255)")
            .IsRequired();

        builder.Property(p => p.Description)
            .HasColumnName("description")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(p => p.Sku)
            .HasColumnName("sku")
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        builder.Property(p => p.IsActive)
            .HasColumnName("is_active")
            .HasColumnType("boolean")
            .HasDefaultValue(true)
            .IsRequired();

        // Money value object mapping
        builder.OwnsOne(p => p.BasePrice, price =>
        {
            price.Property(m => m.Amount)
                .HasColumnName("base_price")
                .HasColumnType("decimal(12, 2)")
                .IsRequired();

            price.Property(m => m.Currency)
                .HasColumnName("currency")
                .HasColumnType("varchar(3)")
                .HasDefaultValue("USD")
                .IsRequired();
        });

        // Slug value object mapping
        builder.OwnsOne(p => p.Slug, slug =>
        {
            slug.Property(s => s.Value)
                .HasColumnName("slug")
                .HasColumnType("varchar(255)")
                .IsRequired();

            slug.HasIndex(s => s.Value)
                .IsUnique();
        });

        // Metadata as JSON
        builder.Property(a => a.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, new JsonSerializerOptions { WriteIndented = false }),
                v => v == null
                    ? new Dictionary<string, object>()
                    : JsonSerializer.Deserialize<Dictionary<string, object>>(v,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            Converters = { new JsonStringEnumConverter() }
                        }) ?? new Dictionary<string, object>()
            );

        // Relationships
        builder.HasMany(p => p.Categories)
            .WithOne()
            .HasForeignKey("ProductId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Attributes)
            .WithOne()
            .HasForeignKey("ProductId")
            .OnDelete(DeleteBehavior.Cascade);


        // Indexes
        builder.HasIndex(p => p.Name);
        builder.HasIndex(p => p.Sku)
            .IsUnique()
            .HasFilter("sku IS NOT NULL");
        builder.HasIndex(p => p.IsActive);
        builder.HasIndex(a => a.Metadata)
            .HasMethod("gin");

        // Constraints
        builder.HasCheckConstraint("check_positive_price", "base_price >= 0");

        // Optimistic Concurrency
        builder.Property(p => p.Version)
            .HasColumnName("version")
            .HasDefaultValue(0)
            .IsConcurrencyToken();


        builder.OwnsMany(p => p.Images, images =>
        {
            images.ToTable("product_images");

            images.WithOwner().HasForeignKey("ProductId");
            images.Property("ProductId")
                .HasColumnName("product_id")
                .HasColumnType("uuid")
                .IsRequired();

            images.Property(i => i.ImageKey)
                .HasColumnName("image_key")
                .IsRequired();

            images.Property(i => i.ThumbnailKey)
                .HasColumnName("thumbnail_key")
                .IsRequired();

            images.Property(i => i.AltText)
                .HasColumnName("alt_text")
                .IsRequired(false);

            images.Property(i => i.IsDefault)
                .HasColumnName("is_default")
                .HasDefaultValue(false)
                .IsRequired();

            images.Property(i => i.DisplayOrder)
                .HasColumnName("display_order")
                .HasDefaultValue(0)
                .IsRequired();

            // Create a composite key with product ID and URL
            images.HasKey("ProductId", "ImageKey");
        });
    }
}
