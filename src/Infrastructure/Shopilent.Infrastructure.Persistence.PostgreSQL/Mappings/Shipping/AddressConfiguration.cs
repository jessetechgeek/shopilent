using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shopilent.Domain.Shipping;
using Shopilent.Domain.Shipping.Enums;

namespace Shopilent.Infrastructure.Persistence.PostgreSQL.Mappings.Shipping;

public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.ToTable("addresses");

        builder.HasKey(a => a.Id);

        // Base entity properties
        builder.Property(a => a.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(a => a.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // Auditable entity properties
        builder.Property(a => a.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("uuid");

        builder.Property(a => a.ModifiedBy)
            .HasColumnName("modified_by")
            .HasColumnType("uuid");

        builder.Property(a => a.LastModified)
            .HasColumnName("last_modified")
            .HasColumnType("timestamp with time zone");

        // Address specific properties
        builder.Property(a => a.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(a => a.IsDefault)
            .HasColumnName("is_default")
            .HasColumnType("boolean")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(a => a.AddressType)
            .HasColumnName("address_type")
            .HasColumnType("varchar(50)")
            .HasConversion<string>()
            .HasDefaultValue(AddressType.Shipping)
            .IsRequired();

        // Value object mappings
        builder.OwnsOne(a => a.PostalAddress, pa =>
        {
            pa.Property(p => p.AddressLine1)
                .HasColumnName("address_line1")
                .HasColumnType("varchar(255)")
                .IsRequired();

            pa.Property(p => p.AddressLine2)
                .HasColumnName("address_line2")
                .HasColumnType("varchar(255)")
                .IsRequired(false);

            pa.Property(p => p.City)
                .HasColumnName("city")
                .HasColumnType("varchar(100)")
                .IsRequired();

            pa.Property(p => p.State)
                .HasColumnName("state")
                .HasColumnType("varchar(100)")
                .IsRequired();

            pa.Property(p => p.PostalCode)
                .HasColumnName("postal_code")
                .HasColumnType("varchar(20)")
                .IsRequired();

            pa.Property(p => p.Country)
                .HasColumnName("country")
                .HasColumnType("varchar(100)")
                .IsRequired();
        });

        builder.OwnsOne(a => a.Phone, phone =>
        {
            phone.Property(p => p.Value)
                .HasColumnName("phone")
                .HasColumnType("varchar(50)")
                .IsRequired(false);
        });

        // Relationships
        builder.HasOne<Domain.Identity.User>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => new { a.UserId, a.IsDefault, a.AddressType });

        // Optimistic Concurrency
        builder.Property(a => a.Version)
            .HasColumnName("version")
            .HasDefaultValue(0)
            .IsConcurrencyToken();
    }
}