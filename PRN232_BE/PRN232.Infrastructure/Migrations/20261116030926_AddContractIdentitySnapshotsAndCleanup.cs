using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PRN232.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContractIdentitySnapshotsAndCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BookingIdentityCleanupTasks",
                columns: table => new
                {
                    BookingIdentityCleanupTaskId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ObjectPath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ApplicationId = table.Column<int>(type: "integer", nullable: true),
                    State = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastErrorCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingIdentityCleanupTasks", x => x.BookingIdentityCleanupTaskId);
                    table.CheckConstraint("CK_IdentityCleanup_State", "\"State\" IN ('Pending','Leased','Succeeded','Retained')");
                });

            migrationBuilder.CreateTable(
                name: "BookingIdentityRetentionAudits",
                columns: table => new
                {
                    BookingIdentityRetentionAuditId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApplicationId = table.Column<int>(type: "integer", nullable: true),
                    TaskId = table.Column<int>(type: "integer", nullable: true),
                    Event = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingIdentityRetentionAudits", x => x.BookingIdentityRetentionAuditId);
                });

            migrationBuilder.CreateTable(
                name: "ContractIdentitySnapshots",
                columns: table => new
                {
                    ContractIdentitySnapshotId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContractId = table.Column<int>(type: "integer", nullable: false),
                    SourceApplicationId = table.Column<int>(type: "integer", nullable: false),
                    SourcePersonId = table.Column<int>(type: "integer", nullable: false),
                    SourceRevision = table.Column<int>(type: "integer", nullable: false),
                    SourceApplicationVersion = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    FullName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IdentityNumber = table.Column<string>(type: "text", nullable: false),
                    FrontObjectPath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    BackObjectPath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractIdentitySnapshots", x => x.ContractIdentitySnapshotId);
                    table.ForeignKey(
                        name: "FK_ContractIdentitySnapshots_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "ContractId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingIdentityCleanupTasks_ObjectPath",
                table: "BookingIdentityCleanupTasks",
                column: "ObjectPath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingIdentityCleanupTasks_State_NextAttemptAtUtc",
                table: "BookingIdentityCleanupTasks",
                columns: new[] { "State", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractIdentitySnapshots_ContractId_Position",
                table: "ContractIdentitySnapshots",
                columns: new[] { "ContractId", "Position" },
                unique: true);
            foreach (var table in new[] { "ContractIdentitySnapshots", "BookingIdentityCleanupTasks", "BookingIdentityRetentionAudits" })
                migrationBuilder.Sql($"""
                    ALTER TABLE public."{table}" ENABLE ROW LEVEL SECURITY;
                    REVOKE ALL PRIVILEGES ON TABLE public."{table}" FROM PUBLIC;
                    DO $identity_security$
                    DECLARE api_role text;
                    BEGIN
                        FOREACH api_role IN ARRAY ARRAY['anon','authenticated','service_role'] LOOP
                            IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname=api_role) THEN
                                EXECUTE format('REVOKE ALL PRIVILEGES ON TABLE public.%I FROM %I','{table}',api_role);
                            END IF;
                        END LOOP;
                    END;
                    $identity_security$;
                    """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingIdentityCleanupTasks");

            migrationBuilder.DropTable(
                name: "BookingIdentityRetentionAudits");

            migrationBuilder.DropTable(
                name: "ContractIdentitySnapshots");
        }
    }
}
