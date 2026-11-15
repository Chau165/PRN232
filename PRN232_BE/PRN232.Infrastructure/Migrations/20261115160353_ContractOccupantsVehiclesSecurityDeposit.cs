using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN232.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ContractOccupantsVehiclesSecurityDeposit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PropertyServiceRates_UnitBillingBasis",
                table: "PropertyServiceRates");

            migrationBuilder.AddColumn<decimal>(
                name: "ManualSecurityDepositAmount",
                table: "Contracts",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RegisteredVehicleCount",
                table: "Contracts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SecurityDepositAcknowledgedAtUtc",
                table: "Contracts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SecurityDepositRecordedByManagerId",
                table: "Contracts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PropertyServiceRates_UnitBillingBasis",
                table: "PropertyServiceRates",
                sql: "(\"UnitCode\" = 'kwh' AND \"BillingBasis\" = 'usage') OR (\"UnitCode\" = 'm3' AND \"BillingBasis\" = 'usage') OR (\"UnitCode\" = 'room_month' AND \"BillingBasis\" = 'fixed_room') OR (\"UnitCode\" = 'person_month' AND \"BillingBasis\" = 'occupant') OR (\"UnitCode\" = 'person_month' AND \"BillingBasis\" = 'additional_occupant') OR (\"UnitCode\" = 'vehicle_month' AND \"BillingBasis\" = 'vehicle')");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_SecurityDepositRecordedByManagerId",
                table: "Contracts",
                column: "SecurityDepositRecordedByManagerId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Contracts_ManualSecurityDeposit_Recorded",
                table: "Contracts",
                sql: "\"ManualSecurityDepositAmount\" IS NULL OR (\"ManualSecurityDepositAmount\" >= 0 AND \"SecurityDepositAcknowledgedAtUtc\" IS NOT NULL AND \"SecurityDepositRecordedByManagerId\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Contracts_RegisteredVehicleCount_NonNegative",
                table: "Contracts",
                sql: "\"RegisteredVehicleCount\" IS NULL OR \"RegisteredVehicleCount\" >= 0");

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_Users_SecurityDepositRecordedByManagerId",
                table: "Contracts",
                column: "SecurityDepositRecordedByManagerId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_Users_SecurityDepositRecordedByManagerId",
                table: "Contracts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PropertyServiceRates_UnitBillingBasis",
                table: "PropertyServiceRates");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_SecurityDepositRecordedByManagerId",
                table: "Contracts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Contracts_ManualSecurityDeposit_Recorded",
                table: "Contracts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Contracts_RegisteredVehicleCount_NonNegative",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "ManualSecurityDepositAmount",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "RegisteredVehicleCount",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "SecurityDepositAcknowledgedAtUtc",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "SecurityDepositRecordedByManagerId",
                table: "Contracts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PropertyServiceRates_UnitBillingBasis",
                table: "PropertyServiceRates",
                sql: "(\"UnitCode\" = 'kwh' AND \"BillingBasis\" = 'usage') OR (\"UnitCode\" = 'm3' AND \"BillingBasis\" = 'usage') OR (\"UnitCode\" = 'room_month' AND \"BillingBasis\" = 'fixed_room') OR (\"UnitCode\" = 'person_month' AND \"BillingBasis\" = 'occupant') OR (\"UnitCode\" = 'vehicle_month' AND \"BillingBasis\" = 'vehicle')");
        }
    }
}
