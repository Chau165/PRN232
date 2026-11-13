using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PRN232.Domain.Entities;

namespace PRN232.Infrastructure.Data.Configurations;

public sealed class PayoutAttemptConfiguration : IEntityTypeConfiguration<PayoutAttempts>
{
    public void Configure(EntityTypeBuilder<PayoutAttempts> entity)
    {
        entity.HasKey(item => item.PayoutAttemptId);
        entity.Property(item => item.Amount).HasPrecision(18, 2);
        entity.Property(item => item.EstimatedFee).HasPrecision(18, 2);
        entity.Property(item => item.FeeAmount).HasPrecision(18, 2);
        entity.Property(item => item.Currency).HasMaxLength(3).IsRequired();
        entity.Property(item => item.Provider).HasMaxLength(32).IsRequired();
        entity.Property(item => item.Status).HasMaxLength(32).IsRequired();
        entity.Property(item => item.ReferenceId).HasMaxLength(128).IsRequired();
        entity.Property(item => item.IdempotencyKey).HasMaxLength(128).IsRequired();
        entity.Property(item => item.ProviderPayoutId).HasMaxLength(128);
        entity.Property(item => item.ProviderTransactionId).HasMaxLength(128);
        entity.Property(item => item.BankReference).HasMaxLength(256);
        entity.Property(item => item.RecipientBankBin).HasMaxLength(16).IsRequired();
        entity.Property(item => item.RecipientAccountNumber).HasMaxLength(64).IsRequired();
        entity.Property(item => item.RecipientAccountName).HasMaxLength(256).IsRequired();
        entity.Property(item => item.Description).HasMaxLength(200).IsRequired();
        entity.Property(item => item.ProviderApprovalState).HasMaxLength(64);
        entity.Property(item => item.ProviderTransactionState).HasMaxLength(64);
        entity.Property(item => item.LastErrorCode).HasMaxLength(128);
        entity.Property(item => item.LastErrorMessage).HasMaxLength(1000);
        entity.HasIndex(item => item.ReferenceId).IsUnique();
        entity.HasIndex(item => item.IdempotencyKey).IsUnique();
        entity.HasIndex(item => new { item.Provider, item.ProviderPayoutId }).IsUnique().HasFilter("\"ProviderPayoutId\" IS NOT NULL");
        entity.HasIndex(item => new { item.Provider, item.ProviderTransactionId }).IsUnique().HasFilter("\"ProviderTransactionId\" IS NOT NULL");
        entity.HasIndex(item => new { item.BookingRefundRequestId, item.AttemptNumber }).IsUnique();
        // Success keeps the slot forever: no second transfer after an already successful one.
        entity.HasIndex(item => item.BookingRefundRequestId).IsUnique()
            .HasDatabaseName("UX_PayoutAttempts_RefundRequestId_Claimed")
            .HasFilter("\"Status\" IN ('Creating', 'Processing', 'RequiresReview', 'Succeeded')");
        entity.HasIndex(item => new { item.Status, item.UpdatedAtUtc });
        entity.HasOne(item => item.BookingRefundRequest).WithMany(item => item.PayoutAttempts)
            .HasForeignKey(item => item.BookingRefundRequestId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.InitiatedByAdmin).WithMany(item => item.PayoutAttemptsInitiated)
            .HasForeignKey(item => item.InitiatedByAdminId).OnDelete(DeleteBehavior.Restrict);
        entity.ToTable("PayoutAttempts", table =>
        {
            table.HasCheckConstraint("CK_PayoutAttempts_Amount",
                "CAST(\"Amount\" AS NUMERIC) > 0 AND CAST(\"Amount\" AS NUMERIC) <= 9007199254740991 AND CAST(\"Amount\" AS NUMERIC) = round(CAST(\"Amount\" AS NUMERIC), 0) AND " +
                "(\"EstimatedFee\" IS NULL OR CAST(\"EstimatedFee\" AS NUMERIC) >= 0) AND (\"FeeAmount\" IS NULL OR CAST(\"FeeAmount\" AS NUMERIC) >= 0)");
            table.HasCheckConstraint("CK_PayoutAttempts_CurrencyProvider", "\"Currency\" = 'VND' AND \"Provider\" = 'PayOS'");
            table.HasCheckConstraint("CK_PayoutAttempts_Identifiers",
                "\"AttemptNumber\" > 0 AND length(trim(\"ReferenceId\")) > 0 AND length(trim(\"IdempotencyKey\")) > 0 AND " +
                "(\"ProviderPayoutId\" IS NULL OR length(trim(\"ProviderPayoutId\")) > 0) AND " +
                "(\"ProviderTransactionId\" IS NULL OR length(trim(\"ProviderTransactionId\")) > 0)");
            table.HasCheckConstraint("CK_PayoutAttempts_Destination",
                "length(trim(\"RecipientBankBin\")) > 0 AND length(trim(\"RecipientAccountNumber\")) > 0 AND " +
                "length(trim(\"RecipientAccountName\")) > 0 AND length(trim(\"Description\")) > 0");
            table.HasCheckConstraint("CK_PayoutAttempts_Times",
                "\"UpdatedAtUtc\" >= \"CreatedAtUtc\" AND " +
                "(\"SubmittedAtUtc\" IS NULL OR (\"SubmittedAtUtc\" >= \"CreatedAtUtc\" AND \"SubmittedAtUtc\" <= \"UpdatedAtUtc\")) AND " +
                "(\"LastCheckedAtUtc\" IS NULL OR (\"LastCheckedAtUtc\" >= \"CreatedAtUtc\" AND \"LastCheckedAtUtc\" <= \"UpdatedAtUtc\")) AND " +
                "(\"CompletedAtUtc\" IS NULL OR (\"CompletedAtUtc\" >= \"CreatedAtUtc\" AND \"CompletedAtUtc\" <= \"UpdatedAtUtc\" AND (\"SubmittedAtUtc\" IS NULL OR \"CompletedAtUtc\" >= \"SubmittedAtUtc\"))) AND " +
                "(\"TransferredAtUtc\" IS NULL OR (\"Status\" = 'Succeeded' AND \"CompletedAtUtc\" IS NOT NULL AND \"TransferredAtUtc\" >= \"CreatedAtUtc\" AND \"TransferredAtUtc\" <= \"CompletedAtUtc\"))");
            table.HasCheckConstraint("CK_PayoutAttempts_State",
                "(\"Status\" IN ('Creating', 'RequiresReview') AND \"CompletedAtUtc\" IS NULL) OR " +
                "(\"Status\" = 'Processing' AND \"ProviderPayoutId\" IS NOT NULL AND \"SubmittedAtUtc\" IS NOT NULL AND \"CompletedAtUtc\" IS NULL) OR " +
                "(\"Status\" = 'Succeeded' AND \"ProviderPayoutId\" IS NOT NULL AND \"ProviderTransactionId\" IS NOT NULL AND \"SubmittedAtUtc\" IS NOT NULL AND \"CompletedAtUtc\" IS NOT NULL) OR " +
                "(\"Status\" = 'Create-failed' AND \"ProviderPayoutId\" IS NULL AND \"ProviderTransactionId\" IS NULL AND \"CompletedAtUtc\" IS NOT NULL AND \"LastErrorCode\" IS NOT NULL AND length(trim(\"LastErrorCode\")) > 0) OR " +
                "(\"Status\" = 'Transfer-failed' AND \"ProviderPayoutId\" IS NOT NULL AND \"SubmittedAtUtc\" IS NOT NULL AND \"CompletedAtUtc\" IS NOT NULL AND \"LastErrorCode\" IS NOT NULL AND length(trim(\"LastErrorCode\")) > 0)");
        });
    }
}
