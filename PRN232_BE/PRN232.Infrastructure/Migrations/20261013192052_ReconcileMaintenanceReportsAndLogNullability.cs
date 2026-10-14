using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PRN232.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReconcileMaintenanceReportsAndLogNullability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "OldStatus",
                table: "MaintenanceLogs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateTable(
                name: "MaintenanceReports",
                columns: table => new
                {
                    ReportId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestId = table.Column<int>(type: "integer", nullable: false),
                    ReportedBy = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceReports", x => x.ReportId);
                    table.ForeignKey(
                        name: "FK_MaintenanceReports_MaintenanceRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "MaintenanceRequests",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MaintenanceReports_Users_ReportedBy",
                        column: x => x.ReportedBy,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceReportImages",
                columns: table => new
                {
                    ImageId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReportId = table.Column<int>(type: "integer", nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceReportImages", x => x.ImageId);
                    table.ForeignKey(
                        name: "FK_MaintenanceReportImages_MaintenanceReports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "MaintenanceReports",
                        principalColumn: "ReportId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                ALTER TABLE "MaintenanceReports" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "MaintenanceReportImages" ENABLE ROW LEVEL SECURITY;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceReportImages_ReportId",
                table: "MaintenanceReportImages",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceReports_ReportedBy",
                table: "MaintenanceReports",
                column: "ReportedBy");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceReports_RequestId",
                table: "MaintenanceReports",
                column: "RequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "MaintenanceLogs" WHERE "OldStatus" IS NULL) THEN
                        RAISE EXCEPTION 'Cannot revert this migration while MaintenanceLogs contains NULL OldStatus values.';
                    END IF;
                END;
                $$;
                """);

            migrationBuilder.DropTable(
                name: "MaintenanceReportImages");

            migrationBuilder.DropTable(
                name: "MaintenanceReports");

            migrationBuilder.AlterColumn<string>(
                name: "OldStatus",
                table: "MaintenanceLogs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
