using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PRN232.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingRefundLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BookingRefunds",
                columns: table => new
                {
                    BookingRefundId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BookingRefundRequestId = table.Column<int>(type: "integer", nullable: false),
                    PayoutAttemptId = table.Column<int>(type: "integer", nullable: false),
                    OriginalPaymentId = table.Column<int>(type: "integer", nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TransferredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingRefunds", x => x.BookingRefundId);
                    table.CheckConstraint("CK_BookingRefunds_Amount", "CAST(\"Amount\" AS NUMERIC) > 0 AND CAST(\"Amount\" AS NUMERIC) <= 9007199254740991 AND CAST(\"Amount\" AS NUMERIC) = round(CAST(\"Amount\" AS NUMERIC), 0)");
                    table.CheckConstraint("CK_BookingRefunds_Currency", "\"Currency\" = 'VND'");
                    table.CheckConstraint("CK_BookingRefunds_Times", "\"TransferredAtUtc\" IS NULL OR \"TransferredAtUtc\" <= \"RecordedAtUtc\"");
                    table.ForeignKey(
                        name: "FK_BookingRefunds_BookingRefundRequests_BookingRefundRequestId",
                        column: x => x.BookingRefundRequestId,
                        principalTable: "BookingRefundRequests",
                        principalColumn: "BookingRefundRequestId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookingRefunds_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "BookingId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookingRefunds_Payments_OriginalPaymentId",
                        column: x => x.OriginalPaymentId,
                        principalTable: "Payments",
                        principalColumn: "PaymentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookingRefunds_PayoutAttempts_PayoutAttemptId",
                        column: x => x.PayoutAttemptId,
                        principalTable: "PayoutAttempts",
                        principalColumn: "PayoutAttemptId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookingRefunds_Users_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingRefunds_BookingId",
                table: "BookingRefunds",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingRefunds_BookingRefundRequestId",
                table: "BookingRefunds",
                column: "BookingRefundRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingRefunds_CustomerId",
                table: "BookingRefunds",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingRefunds_OriginalPaymentId",
                table: "BookingRefunds",
                column: "OriginalPaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingRefunds_PayoutAttemptId",
                table: "BookingRefunds",
                column: "PayoutAttemptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingRefunds_RecordedAtUtc",
                table: "BookingRefunds",
                column: "RecordedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingRefunds");
        }
    }
}
