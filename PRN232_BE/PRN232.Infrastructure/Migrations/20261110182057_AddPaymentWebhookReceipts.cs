using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PRN232.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentWebhookReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentWebhookReceipts",
                columns: table => new
                {
                    PaymentWebhookReceiptId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Reference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    OrderCode = table.Column<long>(type: "bigint", nullable: false),
                    PaymentLinkId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DataCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TransactionDateTime = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ConflictingFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaymentRequestId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentWebhookReceipts", x => x.PaymentWebhookReceiptId);
                    table.ForeignKey(
                        name: "FK_PaymentWebhookReceipts_PaymentRequests_PaymentRequestId",
                        column: x => x.PaymentRequestId,
                        principalTable: "PaymentRequests",
                        principalColumn: "PaymentRequestId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentWebhookReceipts_PaymentRequestId",
                table: "PaymentWebhookReceipts",
                column: "PaymentRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentWebhookReceipts_Provider_Reference",
                table: "PaymentWebhookReceipts",
                columns: new[] { "Provider", "Reference" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentWebhookReceipts");
        }
    }
}
