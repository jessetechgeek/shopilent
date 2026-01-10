using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Payments;
using Shopilent.Domain.Payments.Enums;

namespace Shopilent.Infrastructure.Persistence.PostgreSQL.Mappings.Payments;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");

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

        // Payment specific properties
        builder.Property(p => p.OrderId)
            .HasColumnName("order_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(p => p.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid");

        builder.Property(p => p.Currency)
            .HasColumnName("currency")
            .HasColumnType("varchar(3)")
            .HasDefaultValue("USD")
            .IsRequired();

        builder.Property(p => p.MethodType)
            .HasColumnName("method")
            .HasColumnType("varchar(50)")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(p => p.Provider)
            .HasColumnName("provider")
            .HasColumnType("varchar(50)")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(50)")
            .HasConversion<string>()
            .HasDefaultValue(PaymentStatus.Pending)
            .IsRequired();

        builder.Property(p => p.ExternalReference)
            .HasColumnName("external_reference")
            .HasColumnType("varchar(255)");

        builder.Property(p => p.TransactionId)
            .HasColumnName("transaction_id")
            .HasColumnType("varchar(255)")
            .IsRequired(false);

        builder.Property(p => p.PaymentMethodId)
            .HasColumnName("payment_method_id")
            .HasColumnType("uuid");

        builder.Property(p => p.ProcessedAt)
            .HasColumnName("processed_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.Property(p => p.ErrorMessage)
            .HasColumnName("error_message")
            .HasColumnType("text")
            .IsRequired(false);

        // Money value object mapping
        builder.OwnsOne(p => p.Amount, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("amount")
                .HasColumnType("decimal(12, 2)")
                .IsRequired();

            money.Property(m => m.Currency)
                .HasColumnName("amount_currency")
                .HasColumnType("varchar(3)")
                .HasDefaultValue("USD")
                .IsRequired();
        });

        // Metadata as JSON
        builder.Property(p => p.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, new JsonSerializerOptions { WriteIndented = false }),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, new JsonSerializerOptions { })
            )
            .HasDefaultValue(new Dictionary<string, object>())
            .IsRequired();

        // Relationships
        builder.HasOne<Domain.Sales.Order>()
            .WithMany()
            .HasForeignKey(p => p.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Domain.Identity.User>()
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<PaymentMethod>()
            .WithMany()
            .HasForeignKey(p => p.PaymentMethodId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(p => p.OrderId);
        builder.HasIndex(p => p.UserId);
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.TransactionId);
        builder.HasIndex(p => p.ExternalReference);
        builder.HasIndex(p => p.CreatedAt);
        builder.HasIndex(p => p.ProcessedAt);

        // Constraints
        builder.HasCheckConstraint("check_positive_payment_amount", "amount > 0");

        // Optimistic Concurrency
        builder.Property(p => p.Version)
            .HasColumnName("version")
            .HasDefaultValue(0)
            .IsConcurrencyToken();
    }
}