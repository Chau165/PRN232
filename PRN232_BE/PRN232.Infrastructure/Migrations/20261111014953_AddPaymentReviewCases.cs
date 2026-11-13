using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PRN232.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentReviewCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentReviewCases",
                columns: table => new
                {
                    PaymentReviewCaseId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaymentWebhookReceiptId = table.Column<int>(type: "integer", nullable: false),
                    InvoiceId = table.Column<int>(type: "integer", nullable: false),
                    ExistingPaymentId = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Resolution = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ResolutionNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    EvidenceReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ResolvedByAdminId = table.Column<int>(type: "integer", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentReviewCases", x => x.PaymentReviewCaseId);
                    table.CheckConstraint("CK_PaymentReviewCases_State", "(\"Status\" = 'Open' AND \"Resolution\" IS NULL AND \"ResolvedAtUtc\" IS NULL AND \"ResolvedByAdminId\" IS NULL) OR (\"Status\" = 'Resolved' AND \"Resolution\" IN ('RefundedExternally', 'NotDuplicate') AND \"ResolutionNote\" IS NOT NULL AND \"ResolvedAtUtc\" IS NOT NULL AND \"ResolvedByAdminId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_PaymentReviewCases_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "InvoiceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentReviewCases_PaymentWebhookReceipts_PaymentWebhookRec~",
                        column: x => x.PaymentWebhookReceiptId,
                        principalTable: "PaymentWebhookReceipts",
                        principalColumn: "PaymentWebhookReceiptId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentReviewCases_Payments_ExistingPaymentId",
                        column: x => x.ExistingPaymentId,
                        principalTable: "Payments",
                        principalColumn: "PaymentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentReviewCases_Users_ResolvedByAdminId",
                        column: x => x.ResolvedByAdminId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReviewCases_ExistingPaymentId",
                table: "PaymentReviewCases",
                column: "ExistingPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReviewCases_InvoiceId",
                table: "PaymentReviewCases",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReviewCases_PaymentWebhookReceiptId",
                table: "PaymentReviewCases",
                column: "PaymentWebhookReceiptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReviewCases_ResolvedByAdminId",
                table: "PaymentReviewCases",
                column: "ResolvedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReviewCases_Status_CreatedAtUtc",
                table: "PaymentReviewCases",
                columns: new[] { "Status", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentReviewCases");
        }
    }
}
