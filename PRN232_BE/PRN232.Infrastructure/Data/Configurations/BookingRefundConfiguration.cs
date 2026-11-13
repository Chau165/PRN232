using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PRN232.Domain.Entities;

namespace PRN232.Infrastructure.Data.Configurations;

public sealed class BookingRefundConfiguration : IEntityTypeConfiguration<BookingRefunds>
{
    public void Configure(EntityTypeBuilder<BookingRefunds> entity)
    {
        entity.HasKey(item => item.BookingRefundId);
        entity.Property(item => item.Amount).HasPrecision(18, 2);
        entity.Property(item => item.Currency).HasMaxLength(3).IsRequired();
        entity.HasIndex(item => item.BookingRefundRequestId).IsUnique();
        entity.HasIndex(item => item.PayoutAttemptId).IsUnique();
        entity.HasIndex(item => item.OriginalPaymentId).IsUnique();
        entity.HasIndex(item => item.BookingId).IsUnique();
        entity.HasIndex(item => item.RecordedAtUtc);
        entity.HasOne(item => item.BookingRefundRequest).WithMany().HasForeignKey(item => item.BookingRefundRequestId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.PayoutAttempt).WithMany().HasForeignKey(item => item.PayoutAttemptId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.OriginalPayment).WithMany().HasForeignKey(item => item.OriginalPaymentId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.Booking).WithMany().HasForeignKey(item => item.BookingId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.Customer).WithMany().HasForeignKey(item => item.CustomerId).OnDelete(DeleteBehavior.Restrict);
        entity.ToTable("BookingRefunds", table =>
        {
            table.HasCheckConstraint("CK_BookingRefunds_Amount", "CAST(\"Amount\" AS NUMERIC) > 0 AND CAST(\"Amount\" AS NUMERIC) <= 9007199254740991 AND CAST(\"Amount\" AS NUMERIC) = round(CAST(\"Amount\" AS NUMERIC), 0)");
            table.HasCheckConstraint("CK_BookingRefunds_Currency", "\"Currency\" = 'VND'");
            table.HasCheckConstraint("CK_BookingRefunds_Times", "\"TransferredAtUtc\" IS NULL OR \"TransferredAtUtc\" <= \"RecordedAtUtc\"");
        });
    }
}
