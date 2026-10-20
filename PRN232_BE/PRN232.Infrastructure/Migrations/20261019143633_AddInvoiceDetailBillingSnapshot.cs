using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN232.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceDetailBillingSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BillingBasis",
                table: "InvoiceDetails",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Quantity",
                table: "InvoiceDetails",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RateId",
                table: "InvoiceDetails",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ServiceId",
                table: "InvoiceDetails",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitCode",
                table: "InvoiceDetails",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPrice",
                table: "InvoiceDetails",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDetails_RateId_ServiceId",
                table: "InvoiceDetails",
                columns: new[] { "RateId", "ServiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDetails_ServiceId",
                table: "InvoiceDetails",
                column: "ServiceId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvoiceDetails_RateRequiresService",
                table: "InvoiceDetails",
                sql: "\"RateId\" IS NULL OR \"ServiceId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceDetails_PropertyServiceRates_RateId_ServiceId",
                table: "InvoiceDetails",
                columns: new[] { "RateId", "ServiceId" },
                principalTable: "PropertyServiceRates",
                principalColumns: new[] { "RateId", "ServiceId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceDetails_Services_ServiceId",
                table: "InvoiceDetails",
                column: "ServiceId",
                principalTable: "Services",
                principalColumn: "ServiceId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "InvoiceDetails"
                        WHERE "ServiceId" IS NOT NULL
                           OR "RateId" IS NOT NULL
                           OR "Quantity" IS NOT NULL
                           OR "UnitPrice" IS NOT NULL
                           OR "UnitCode" IS NOT NULL
                           OR "BillingBasis" IS NOT NULL
                    ) THEN
                        RAISE EXCEPTION 'Cannot revert invoice snapshot columns while any line contains snapshot data.';
                    END IF;
                END;
                $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceDetails_PropertyServiceRates_RateId_ServiceId",
                table: "InvoiceDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceDetails_Services_ServiceId",
                table: "InvoiceDetails");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceDetails_RateId_ServiceId",
                table: "InvoiceDetails");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceDetails_ServiceId",
                table: "InvoiceDetails");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InvoiceDetails_RateRequiresService",
                table: "InvoiceDetails");

            migrationBuilder.DropColumn(
                name: "BillingBasis",
                table: "InvoiceDetails");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "InvoiceDetails");

            migrationBuilder.DropColumn(
                name: "RateId",
                table: "InvoiceDetails");

            migrationBuilder.DropColumn(
                name: "ServiceId",
                table: "InvoiceDetails");

            migrationBuilder.DropColumn(
                name: "UnitCode",
                table: "InvoiceDetails");

            migrationBuilder.DropColumn(
                name: "UnitPrice",
                table: "InvoiceDetails");
        }
    }
}
