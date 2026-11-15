using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN232.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingDepositReturned : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DepositReturnedAtUtc",
                table: "Bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DepositReturnedById",
                table: "Bookings",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_DepositReturnedById",
                table: "Bookings",
                column: "DepositReturnedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Users_DepositReturnedById",
                table: "Bookings",
                column: "DepositReturnedById",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Users_DepositReturnedById",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_DepositReturnedById",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "DepositReturnedAtUtc",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "DepositReturnedById",
                table: "Bookings");
        }
    }
}
