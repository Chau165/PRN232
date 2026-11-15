using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN232.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingMoveInDateAndOccupantCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "MoveInDate",
                table: "Bookings",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OccupantCount",
                table: "Bookings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bookings_OccupantCount_Range",
                table: "Bookings",
                sql: "\"OccupantCount\" IS NULL OR \"OccupantCount\" BETWEEN 1 AND 10");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Bookings_OccupantCount_Range",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "MoveInDate",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "OccupantCount",
                table: "Bookings");
        }
    }
}
