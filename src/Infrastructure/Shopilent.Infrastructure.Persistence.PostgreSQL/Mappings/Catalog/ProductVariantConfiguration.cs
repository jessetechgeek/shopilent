using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shopilent.Domain.Catalog;

namespace Shopilent.Infrastructure.Persistence.PostgreSQL.Mappings.Catalog;

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("product_variants");

        builder.HasKey(pv => pv.Id);

        // Base entity properties
        builder.Property(pv => pv.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(pv => pv.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(pv => pv.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // Auditable entity properties
        builder.Property(pv => pv.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("uuid");

        builder.Property(pv => pv.ModifiedBy)
            .HasColumnName("modified_by")
            .HasColumnType("uuid");

        builder.Property(pv => pv.LastModified)
            .HasColumnName("last_modified")
            .HasColumnType("timestamp with time zone");

        // Product variant specific properties
        builder.Property(pv => pv.ProductId)
            .HasColumnName("product_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(pv => pv.Sku)
            .HasColumnName("sku")
            .HasColumnType("varchar(100)");

        builder.Property(pv => pv.StockQuantity)
            .HasColumnName("stock_quantity")
            .HasColumnType("integer")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(pv => pv.IsActive)
            .HasColumnName("is_active")
            .HasColumnType("boolean")
            .HasDefaultValue(true)
            .IsRequired();

        // Money value object mapping
        builder.OwnsOne(pv => pv.Price, price =>
        {
            price.Property(m => m.Amount)
                .HasColumnName("price")
                .HasColumnType("decimal(12, 2)");

            price.Property(m => m.Currency)
                .HasColumnName("currency")
                .HasColumnType("varchar(3)")
                .HasDefaultValue("USD");
        });

        // Metadata as JSON
        builder.Property(pv => pv.Metadata)
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

        builder.Property(pv => pv.Version)
            .HasColumnName("version")
            .HasDefaultValue(0)
            .IsConcurrencyToken();

        // Relationships
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(pv => pv.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(pv => pv.VariantAttributes)
            .WithOne()
            .HasForeignKey("VariantId")
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(pv => pv.ProductId);
        builder.HasIndex(pv => pv.Sku)
            .IsUnique()
            .HasFilter("sku IS NOT NULL");
        builder.HasIndex(pv => pv.IsActive);
        builder.HasIndex(a => a.Metadata)
            .HasMethod("gin");

        // Constraints
        builder.HasCheckConstraint("check_positive_variant_price", "price >= 0");
        builder.HasCheckConstraint("check_positive_stock", "stock_quantity >= 0");

        // Optimistic Concurrency
        builder.Property(pv => pv.Version)
            .HasColumnName("version")
            .HasDefaultValue(0)
            .IsConcurrencyToken();

        builder.OwnsMany(v => v.Images, images =>
        {
            images.ToTable("product_variant_images");

            images.WithOwner().HasForeignKey("VariantId");
            images.Property("VariantId")
                .HasColumnName("variant_id")
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

            // Create a composite key with variant ID and URL
            images.HasKey("VariantId", "ImageKey");
        });
    }
}
