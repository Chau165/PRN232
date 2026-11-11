using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PRN232.Domain.Entities;

namespace PRN232.Infrastructure.Data.Configurations;

public sealed class BookingRefundRequestConfiguration : IEntityTypeConfiguration<BookingRefundRequests>
{
    public void Configure(EntityTypeBuilder<BookingRefundRequests> entity)
    {
        entity.HasKey(item => item.BookingRefundRequestId);
        entity.Property(item => item.RequestedAmount).HasPrecision(18, 2);
        entity.Property(item => item.ApprovedAmount).HasPrecision(18, 2);
        entity.Property(item => item.Currency).HasMaxLength(3).IsRequired();
        entity.Property(item => item.Status).HasMaxLength(32).IsRequired();
        entity.Property(item => item.Reason).HasMaxLength(2000).IsRequired();
        entity.Property(item => item.RecipientBankBin).HasMaxLength(16).IsRequired();
        entity.Property(item => item.RecipientAccountNumber).HasMaxLength(64).IsRequired();
        entity.Property(item => item.RecipientAccountName).HasMaxLength(256);
        entity.Property(item => item.ReviewNote).HasMaxLength(2000);
        const string claimed = "\"Status\" IN ('PendingReview', 'Approved', 'Refunding', 'RequiresReview', 'Refunded')";
        entity.HasIndex(item => item.BookingId).IsUnique()
            .HasDatabaseName("UX_BookingRefundRequests_BookingId_Claimed").HasFilter(claimed);
        entity.HasIndex(item => item.OriginalPaymentId).IsUnique()
            .HasDatabaseName("UX_BookingRefundRequests_OriginalPaymentId_Claimed").HasFilter(claimed);
        entity.HasIndex(item => new { item.Status, item.CreatedAtUtc });
        entity.HasIndex(item => new { item.CustomerId, item.CreatedAtUtc });
        entity.HasOne(item => item.Booking).WithMany(item => item.RefundRequests)
            .HasForeignKey(item => item.BookingId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.Customer).WithMany(item => item.BookingRefundRequests)
            .HasForeignKey(item => item.CustomerId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.OriginalPayment).WithMany(item => item.BookingRefundRequests)
            .HasForeignKey(item => item.OriginalPaymentId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.ReviewedByAdmin).WithMany(item => item.BookingRefundRequestsReviewed)
            .HasForeignKey(item => item.ReviewedByAdminId).OnDelete(DeleteBehavior.Restrict);

        const string approved = "\"ApprovedAmount\" IS NOT NULL AND \"ReviewedByAdminId\" IS NOT NULL AND \"ReviewedAtUtc\" IS NOT NULL " +
            "AND \"RecipientAccountName\" IS NOT NULL AND length(trim(\"RecipientAccountName\")) > 0";
        entity.ToTable("BookingRefundRequests", table =>
        {
            table.HasCheckConstraint("CK_BookingRefundRequests_Amount",
                "CAST(\"RequestedAmount\" AS NUMERIC) > 0 AND CAST(\"RequestedAmount\" AS NUMERIC) <= 9007199254740991 AND CAST(\"RequestedAmount\" AS NUMERIC) = round(CAST(\"RequestedAmount\" AS NUMERIC), 0) AND " +
                "(\"ApprovedAmount\" IS NULL OR (CAST(\"ApprovedAmount\" AS NUMERIC) > 0 AND CAST(\"ApprovedAmount\" AS NUMERIC) <= CAST(\"RequestedAmount\" AS NUMERIC) AND CAST(\"ApprovedAmount\" AS NUMERIC) = round(CAST(\"ApprovedAmount\" AS NUMERIC), 0)))");
            table.HasCheckConstraint("CK_BookingRefundRequests_Currency", "\"Currency\" = 'VND'");
            table.HasCheckConstraint("CK_BookingRefundRequests_Destination",
                "length(trim(\"RecipientBankBin\")) > 0 AND length(trim(\"RecipientAccountNumber\")) > 0 AND length(trim(\"Reason\")) > 0");
            table.HasCheckConstraint("CK_BookingRefundRequests_Times",
                "\"UpdatedAtUtc\" >= \"CreatedAtUtc\" AND " +
                "(\"ReviewedAtUtc\" IS NULL OR (\"ReviewedAtUtc\" >= \"CreatedAtUtc\" AND \"ReviewedAtUtc\" <= \"UpdatedAtUtc\")) AND " +
                "(\"RefundedAtUtc\" IS NULL OR (\"ReviewedAtUtc\" IS NOT NULL AND \"RefundedAtUtc\" >= \"ReviewedAtUtc\" AND \"RefundedAtUtc\" <= \"UpdatedAtUtc\"))");
            table.HasCheckConstraint("CK_BookingRefundRequests_State",
                "(\"Status\" = 'PendingReview' AND \"ApprovedAmount\" IS NULL AND \"ReviewedByAdminId\" IS NULL AND \"ReviewedAtUtc\" IS NULL AND \"RefundedAtUtc\" IS NULL) OR " +
                "(\"Status\" = 'Rejected' AND \"ApprovedAmount\" IS NULL AND \"ReviewedByAdminId\" IS NOT NULL AND \"ReviewedAtUtc\" IS NOT NULL AND \"RefundedAtUtc\" IS NULL " +
                "AND \"ReviewNote\" IS NOT NULL AND length(trim(\"ReviewNote\")) > 0) OR " +
                $"(\"Status\" IN ('Approved', 'Refunding', 'RequiresReview') AND {approved} AND \"RefundedAtUtc\" IS NULL) OR " +
                $"(\"Status\" = 'Refunded' AND {approved} AND \"RefundedAtUtc\" IS NOT NULL)");
        });
    }
}
