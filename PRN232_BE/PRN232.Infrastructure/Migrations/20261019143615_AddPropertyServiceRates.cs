using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PRN232.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertyServiceRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PropertyServiceRates",
                columns: table => new
                {
                    RateId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PropertyId = table.Column<int>(type: "integer", nullable: false),
                    ServiceId = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    UnitCode = table.Column<string>(type: "text", nullable: false),
                    BillingBasis = table.Column<string>(type: "text", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyServiceRates", x => x.RateId);
                    table.UniqueConstraint("AK_PropertyServiceRates_RateId_ServiceId", x => new { x.RateId, x.ServiceId });
                    table.CheckConstraint("CK_PropertyServiceRates_EffectiveFrom_MonthStart", "EXTRACT(DAY FROM \"EffectiveFrom\") = 1");
                    table.CheckConstraint("CK_PropertyServiceRates_EffectiveRange", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" > \"EffectiveFrom\"");
                    table.CheckConstraint("CK_PropertyServiceRates_EffectiveTo_MonthStart", "\"EffectiveTo\" IS NULL OR EXTRACT(DAY FROM \"EffectiveTo\") = 1");
                    table.CheckConstraint("CK_PropertyServiceRates_Price_NonNegative", "\"Price\" >= 0");
                    table.CheckConstraint("CK_PropertyServiceRates_UnitBillingBasis", "(\"UnitCode\" = 'kwh' AND \"BillingBasis\" = 'usage') OR (\"UnitCode\" = 'm3' AND \"BillingBasis\" = 'usage') OR (\"UnitCode\" = 'room_month' AND \"BillingBasis\" = 'fixed_room') OR (\"UnitCode\" = 'person_month' AND \"BillingBasis\" = 'occupant') OR (\"UnitCode\" = 'vehicle_month' AND \"BillingBasis\" = 'vehicle')");
                    table.ForeignKey(
                        name: "FK_PropertyServiceRates_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "PropertyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PropertyServiceRates_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "ServiceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyServiceRates_PropertyId_ServiceId_EffectiveFrom",
                table: "PropertyServiceRates",
                columns: new[] { "PropertyId", "ServiceId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyServiceRates_ServiceId",
                table: "PropertyServiceRates",
                column: "ServiceId");

            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");
            migrationBuilder.Sql("""
                ALTER TABLE "PropertyServiceRates"
                ADD CONSTRAINT "EX_PropertyServiceRates_NoOverlappingPeriods"
                EXCLUDE USING gist (
                    "PropertyId" WITH =,
                    "ServiceId" WITH =,
                    daterange("EffectiveFrom", "EffectiveTo", '[)') WITH &&
                );

                ALTER TABLE "PropertyServiceRates" ENABLE ROW LEVEL SECURITY;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "PropertyServiceRates") THEN
                        RAISE EXCEPTION 'Cannot revert PropertyServiceRates while rate history exists.';
                    END IF;
                END;
                $$;
                """);

            migrationBuilder.DropTable(
                name: "PropertyServiceRates");
        }
    }
}
