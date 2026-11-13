using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN232.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContractMeterHandoverBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "InitialElectricReading",
                table: "Contracts",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InitialWaterReading",
                table: "Contracts",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "MeterHandoverDate",
                table: "Contracts",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InitialElectricReading",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "InitialWaterReading",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "MeterHandoverDate",
                table: "Contracts");
        }
    }
}
