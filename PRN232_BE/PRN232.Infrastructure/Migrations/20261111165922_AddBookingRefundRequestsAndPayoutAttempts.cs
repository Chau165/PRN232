using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PRN232.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingRefundRequestsAndPayoutAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BookingRefundRequests",
                columns: table => new
                {
                    BookingRefundRequestId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    OriginalPaymentId = table.Column<int>(type: "integer", nullable: false),
                    RequestedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ApprovedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RecipientBankBin = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RecipientAccountNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RecipientAccountName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ReviewedByAdminId = table.Column<int>(type: "integer", nullable: true),
                    ReviewNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RefundedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingRefundRequests", x => x.BookingRefundRequestId);
                    table.CheckConstraint("CK_BookingRefundRequests_Amount", "CAST(\"RequestedAmount\" AS NUMERIC) > 0 AND CAST(\"RequestedAmount\" AS NUMERIC) <= 9007199254740991 AND CAST(\"RequestedAmount\" AS NUMERIC) = round(CAST(\"RequestedAmount\" AS NUMERIC), 0) AND (\"ApprovedAmount\" IS NULL OR (CAST(\"ApprovedAmount\" AS NUMERIC) > 0 AND CAST(\"ApprovedAmount\" AS NUMERIC) <= CAST(\"RequestedAmount\" AS NUMERIC) AND CAST(\"ApprovedAmount\" AS NUMERIC) = round(CAST(\"ApprovedAmount\" AS NUMERIC), 0)))");
                    table.CheckConstraint("CK_BookingRefundRequests_Currency", "\"Currency\" = 'VND'");
                    table.CheckConstraint("CK_BookingRefundRequests_Destination", "length(trim(\"RecipientBankBin\")) > 0 AND length(trim(\"RecipientAccountNumber\")) > 0 AND length(trim(\"Reason\")) > 0");
                    table.CheckConstraint("CK_BookingRefundRequests_State", "(\"Status\" = 'PendingReview' AND \"ApprovedAmount\" IS NULL AND \"ReviewedByAdminId\" IS NULL AND \"ReviewedAtUtc\" IS NULL AND \"RefundedAtUtc\" IS NULL) OR (\"Status\" = 'Rejected' AND \"ApprovedAmount\" IS NULL AND \"ReviewedByAdminId\" IS NOT NULL AND \"ReviewedAtUtc\" IS NOT NULL AND \"RefundedAtUtc\" IS NULL AND \"ReviewNote\" IS NOT NULL AND length(trim(\"ReviewNote\")) > 0) OR (\"Status\" IN ('Approved', 'Refunding', 'RequiresReview') AND \"ApprovedAmount\" IS NOT NULL AND \"ReviewedByAdminId\" IS NOT NULL AND \"ReviewedAtUtc\" IS NOT NULL AND \"RecipientAccountName\" IS NOT NULL AND length(trim(\"RecipientAccountName\")) > 0 AND \"RefundedAtUtc\" IS NULL) OR (\"Status\" = 'Refunded' AND \"ApprovedAmount\" IS NOT NULL AND \"ReviewedByAdminId\" IS NOT NULL AND \"ReviewedAtUtc\" IS NOT NULL AND \"RecipientAccountName\" IS NOT NULL AND length(trim(\"RecipientAccountName\")) > 0 AND \"RefundedAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_BookingRefundRequests_Times", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\" AND (\"ReviewedAtUtc\" IS NULL OR (\"ReviewedAtUtc\" >= \"CreatedAtUtc\" AND \"ReviewedAtUtc\" <= \"UpdatedAtUtc\")) AND (\"RefundedAtUtc\" IS NULL OR (\"ReviewedAtUtc\" IS NOT NULL AND \"RefundedAtUtc\" >= \"ReviewedAtUtc\" AND \"RefundedAtUtc\" <= \"UpdatedAtUtc\"))");
                    table.ForeignKey(
                        name: "FK_BookingRefundRequests_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "BookingId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookingRefundRequests_Payments_OriginalPaymentId",
                        column: x => x.OriginalPaymentId,
                        principalTable: "Payments",
                        principalColumn: "PaymentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookingRefundRequests_Users_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookingRefundRequests_Users_ReviewedByAdminId",
                        column: x => x.ReviewedByAdminId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayoutAttempts",
                columns: table => new
                {
                    PayoutAttemptId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BookingRefundRequestId = table.Column<int>(type: "integer", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    InitiatedByAdminId = table.Column<int>(type: "integer", nullable: false),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReferenceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProviderPayoutId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ProviderTransactionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    BankReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    RecipientBankBin = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RecipientAccountNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RecipientAccountName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EstimatedFee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    FeeAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ProviderApprovalState = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ProviderTransactionState = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LastErrorCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LastErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastCheckedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TransferredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayoutAttempts", x => x.PayoutAttemptId);
                    table.CheckConstraint("CK_PayoutAttempts_Amount", "CAST(\"Amount\" AS NUMERIC) > 0 AND CAST(\"Amount\" AS NUMERIC) <= 9007199254740991 AND CAST(\"Amount\" AS NUMERIC) = round(CAST(\"Amount\" AS NUMERIC), 0) AND (\"EstimatedFee\" IS NULL OR CAST(\"EstimatedFee\" AS NUMERIC) >= 0) AND (\"FeeAmount\" IS NULL OR CAST(\"FeeAmount\" AS NUMERIC) >= 0)");
                    table.CheckConstraint("CK_PayoutAttempts_CurrencyProvider", "\"Currency\" = 'VND' AND \"Provider\" = 'PayOS'");
                    table.CheckConstraint("CK_PayoutAttempts_Destination", "length(trim(\"RecipientBankBin\")) > 0 AND length(trim(\"RecipientAccountNumber\")) > 0 AND length(trim(\"RecipientAccountName\")) > 0 AND length(trim(\"Description\")) > 0");
                    table.CheckConstraint("CK_PayoutAttempts_Identifiers", "\"AttemptNumber\" > 0 AND length(trim(\"ReferenceId\")) > 0 AND length(trim(\"IdempotencyKey\")) > 0 AND (\"ProviderPayoutId\" IS NULL OR length(trim(\"ProviderPayoutId\")) > 0) AND (\"ProviderTransactionId\" IS NULL OR length(trim(\"ProviderTransactionId\")) > 0)");
                    table.CheckConstraint("CK_PayoutAttempts_State", "(\"Status\" IN ('Creating', 'RequiresReview') AND \"CompletedAtUtc\" IS NULL) OR (\"Status\" = 'Processing' AND \"ProviderPayoutId\" IS NOT NULL AND \"SubmittedAtUtc\" IS NOT NULL AND \"CompletedAtUtc\" IS NULL) OR (\"Status\" = 'Succeeded' AND \"ProviderPayoutId\" IS NOT NULL AND \"ProviderTransactionId\" IS NOT NULL AND \"SubmittedAtUtc\" IS NOT NULL AND \"CompletedAtUtc\" IS NOT NULL) OR (\"Status\" = 'Create-failed' AND \"ProviderPayoutId\" IS NULL AND \"ProviderTransactionId\" IS NULL AND \"CompletedAtUtc\" IS NOT NULL AND \"LastErrorCode\" IS NOT NULL AND length(trim(\"LastErrorCode\")) > 0) OR (\"Status\" = 'Transfer-failed' AND \"ProviderPayoutId\" IS NOT NULL AND \"SubmittedAtUtc\" IS NOT NULL AND \"CompletedAtUtc\" IS NOT NULL AND \"LastErrorCode\" IS NOT NULL AND length(trim(\"LastErrorCode\")) > 0)");
                    table.CheckConstraint("CK_PayoutAttempts_Times", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\" AND (\"SubmittedAtUtc\" IS NULL OR (\"SubmittedAtUtc\" >= \"CreatedAtUtc\" AND \"SubmittedAtUtc\" <= \"UpdatedAtUtc\")) AND (\"LastCheckedAtUtc\" IS NULL OR (\"LastCheckedAtUtc\" >= \"CreatedAtUtc\" AND \"LastCheckedAtUtc\" <= \"UpdatedAtUtc\")) AND (\"CompletedAtUtc\" IS NULL OR (\"CompletedAtUtc\" >= \"CreatedAtUtc\" AND \"CompletedAtUtc\" <= \"UpdatedAtUtc\" AND (\"SubmittedAtUtc\" IS NULL OR \"CompletedAtUtc\" >= \"SubmittedAtUtc\"))) AND (\"TransferredAtUtc\" IS NULL OR (\"Status\" = 'Succeeded' AND \"CompletedAtUtc\" IS NOT NULL AND \"TransferredAtUtc\" >= \"CreatedAtUtc\" AND \"TransferredAtUtc\" <= \"CompletedAtUtc\"))");
                    table.ForeignKey(
                        name: "FK_PayoutAttempts_BookingRefundRequests_BookingRefundRequestId",
                        column: x => x.BookingRefundRequestId,
                        principalTable: "BookingRefundRequests",
                        principalColumn: "BookingRefundRequestId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayoutAttempts_Users_InitiatedByAdminId",
                        column: x => x.InitiatedByAdminId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingRefundRequests_CustomerId_CreatedAtUtc",
                table: "BookingRefundRequests",
                columns: new[] { "CustomerId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingRefundRequests_ReviewedByAdminId",
                table: "BookingRefundRequests",
                column: "ReviewedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingRefundRequests_Status_CreatedAtUtc",
                table: "BookingRefundRequests",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_BookingRefundRequests_BookingId_Claimed",
                table: "BookingRefundRequests",
                column: "BookingId",
                unique: true,
                filter: "\"Status\" IN ('PendingReview', 'Approved', 'Refunding', 'RequiresReview', 'Refunded')");

            migrationBuilder.CreateIndex(
                name: "UX_BookingRefundRequests_OriginalPaymentId_Claimed",
                table: "BookingRefundRequests",
                column: "OriginalPaymentId",
                unique: true,
                filter: "\"Status\" IN ('PendingReview', 'Approved', 'Refunding', 'RequiresReview', 'Refunded')");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutAttempts_BookingRefundRequestId_AttemptNumber",
                table: "PayoutAttempts",
                columns: new[] { "BookingRefundRequestId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayoutAttempts_IdempotencyKey",
                table: "PayoutAttempts",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayoutAttempts_InitiatedByAdminId",
                table: "PayoutAttempts",
                column: "InitiatedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutAttempts_Provider_ProviderPayoutId",
                table: "PayoutAttempts",
                columns: new[] { "Provider", "ProviderPayoutId" },
                unique: true,
                filter: "\"ProviderPayoutId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutAttempts_Provider_ProviderTransactionId",
                table: "PayoutAttempts",
                columns: new[] { "Provider", "ProviderTransactionId" },
                unique: true,
                filter: "\"ProviderTransactionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutAttempts_ReferenceId",
                table: "PayoutAttempts",
                column: "ReferenceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayoutAttempts_Status_UpdatedAtUtc",
                table: "PayoutAttempts",
                columns: new[] { "Status", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_PayoutAttempts_RefundRequestId_Claimed",
                table: "PayoutAttempts",
                column: "BookingRefundRequestId",
                unique: true,
                filter: "\"Status\" IN ('Creating', 'Processing', 'RequiresReview', 'Succeeded')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayoutAttempts");

            migrationBuilder.DropTable(
                name: "BookingRefundRequests");
        }
    }
}
