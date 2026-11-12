using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN232.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingPaymentRequestTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_PaymentRequests_InvoiceId_Active",
                table: "PaymentRequests");

            migrationBuilder.AlterColumn<int>(
                name: "InvoiceId",
                table: "PaymentRequests",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "BookingId",
                table: "PaymentRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_PaymentRequests_BookingId_Active",
                table: "PaymentRequests",
                column: "BookingId",
                unique: true,
                filter: "\"BookingId\" IS NOT NULL AND \"Status\" IN ('Creating', 'Pending', 'RequiresReview')");

            migrationBuilder.CreateIndex(
                name: "UX_PaymentRequests_InvoiceId_Active",
                table: "PaymentRequests",
                column: "InvoiceId",
                unique: true,
                filter: "\"InvoiceId\" IS NOT NULL AND \"Status\" IN ('Creating', 'Pending', 'RequiresReview')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentRequests_ExactlyOneTarget",
                table: "PaymentRequests",
                sql: "(\"InvoiceId\" IS NOT NULL AND \"BookingId\" IS NULL) OR (\"InvoiceId\" IS NULL AND \"BookingId\" IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentRequests_Bookings_BookingId",
                table: "PaymentRequests",
                column: "BookingId",
                principalTable: "Bookings",
                principalColumn: "BookingId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentRequests_Bookings_BookingId",
                table: "PaymentRequests");

            migrationBuilder.DropIndex(
                name: "UX_PaymentRequests_BookingId_Active",
                table: "PaymentRequests");

            migrationBuilder.DropIndex(
                name: "UX_PaymentRequests_InvoiceId_Active",
                table: "PaymentRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentRequests_ExactlyOneTarget",
                table: "PaymentRequests");

            migrationBuilder.DropColumn(
                name: "BookingId",
                table: "PaymentRequests");

            migrationBuilder.AlterColumn<int>(
                name: "InvoiceId",
                table: "PaymentRequests",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_PaymentRequests_InvoiceId_Active",
                table: "PaymentRequests",
                column: "InvoiceId",
                unique: true,
                filter: "\"Status\" IN ('Creating', 'Pending', 'RequiresReview')");
        }
    }
}
