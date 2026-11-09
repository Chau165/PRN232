using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN232.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentPaidAtUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PaidAtUtc",
                table: "Payments",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaidAtUtc",
                table: "Payments");
        }
    }
}
