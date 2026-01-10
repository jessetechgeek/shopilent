using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.Enums;

namespace Shopilent.Infrastructure.Persistence.PostgreSQL.Mappings.Sales;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");

        builder.HasKey(o => o.Id);

        // Base entity properties
        builder.Property(o => o.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(o => o.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(o => o.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // Auditable entity properties
        builder.Property(o => o.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("uuid");

        builder.Property(o => o.ModifiedBy)
            .HasColumnName("modified_by")
            .HasColumnType("uuid");

        builder.Property(o => o.LastModified)
            .HasColumnName("last_modified")
            .HasColumnType("timestamp with time zone");

        // Order specific properties
        builder.Property(o => o.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid");

        builder.Property(o => o.BillingAddressId)
            .HasColumnName("billing_address_id")
            .HasColumnType("uuid");

        builder.Property(o => o.ShippingAddressId)
            .HasColumnName("shipping_address_id")
            .HasColumnType("uuid");

        builder.Property(o => o.PaymentMethodId)
            .HasColumnName("payment_method_id")
            .HasColumnType("uuid");

        builder.Property(o => o.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(50)")
            .HasConversion<string>()
            .HasDefaultValue(OrderStatus.Pending)
            .IsRequired();

        builder.Property(o => o.PaymentStatus)
            .HasColumnName("payment_status")
            .HasColumnType("varchar(50)")
            .HasConversion<string>()
            .HasDefaultValue(PaymentStatus.Pending)
            .IsRequired();

        builder.Property(o => o.ShippingMethod)
            .HasColumnName("shipping_method")
            .HasColumnType("varchar(100)");

        builder.Property(o => o.RefundedAt)
            .HasColumnName("refunded_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(o => o.RefundReason)
            .HasColumnName("refund_reason")
            .HasColumnType("text")
            .IsRequired(false);

        // Money value objects
        builder.OwnsOne(o => o.Subtotal, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("subtotal")
                .HasColumnType("decimal(12, 2)")
                .IsRequired();

            money.Property(m => m.Currency)
                .HasColumnName("currency")
                .HasColumnType("varchar(3)")
                .HasDefaultValue("USD")
                .IsRequired();
        });

        builder.OwnsOne(o => o.Tax, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("tax")
                .HasColumnType("decimal(12, 2)")
                .HasDefaultValue(0)
                .IsRequired();

            money.Property(m => m.Currency)
                .HasColumnName("tax_currency")
                .HasColumnType("varchar(3)")
                .HasDefaultValue("USD")
                .IsRequired();
        });

        builder.OwnsOne(o => o.ShippingCost, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("shipping_cost")
                .HasColumnType("decimal(12, 2)")
                .HasDefaultValue(0)
                .IsRequired();

            money.Property(m => m.Currency)
                .HasColumnName("shipping_cost_currency")
                .HasColumnType("varchar(3)")
                .HasDefaultValue("USD")
                .IsRequired();
        });

        builder.OwnsOne(o => o.Total, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("total")
                .HasColumnType("decimal(12, 2)")
                .IsRequired();

            money.Property(m => m.Currency)
                .HasColumnName("total_currency")
                .HasColumnType("varchar(3)")
                .HasDefaultValue("USD")
                .IsRequired();
        });

        builder.OwnsOne(o => o.RefundedAmount, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("refunded_amount")
                .HasColumnType("decimal(12, 2)")
                .HasDefaultValue(0);

            money.Property(m => m.Currency)
                .HasColumnName("refunded_currency")
                .HasColumnType("varchar(3)")
                .HasDefaultValue("USD")
                .IsRequired(false);
        });

        // Metadata as JSON
        builder.Property(o => o.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, new JsonSerializerOptions { WriteIndented = false }),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, new JsonSerializerOptions { })
            );

        // Relationships
        builder.HasOne<Domain.Identity.User>()
            .WithMany(u => u.Orders)
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Domain.Shipping.Address>()
            .WithMany()
            .HasForeignKey(o => o.BillingAddressId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Domain.Shipping.Address>()
            .WithMany()
            .HasForeignKey(o => o.ShippingAddressId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Domain.Payments.PaymentMethod>()
            .WithMany()
            .HasForeignKey(o => o.PaymentMethodId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey("OrderId")
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(o => o.UserId);
        builder.HasIndex(o => o.Status);
        builder.HasIndex(o => o.PaymentStatus);
        builder.HasIndex(o => o.CreatedAt);
        builder.HasIndex(o => o.PaymentMethodId);

        // Constraints
        builder.HasCheckConstraint("check_positive_subtotal", "subtotal >= 0");
        builder.HasCheckConstraint("check_positive_tax", "tax >= 0");
        builder.HasCheckConstraint("check_positive_shipping", "shipping_cost >= 0");
        builder.HasCheckConstraint("check_positive_total", "total >= 0");

        // Optimistic Concurrency
        builder.Property(o => o.Version)
            .HasColumnName("version")
            .HasDefaultValue(0)
            .IsConcurrencyToken();
    }
}