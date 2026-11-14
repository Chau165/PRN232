using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN232.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PropertyScopedRoomTypesAndViewingFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ViewingSchedules_Rooms_RoomId",
                table: "ViewingSchedules");

            migrationBuilder.DropIndex(
                name: "IX_ViewingSchedules_RoomId",
                table: "ViewingSchedules");

            migrationBuilder.DropIndex(
                name: "IX_Rooms_PropertyId",
                table: "Rooms");

            migrationBuilder.AlterColumn<int>(
                name: "RoomId",
                table: "ViewingSchedules",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "PropertyId",
                table: "ViewingSchedules",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RoomTypeId",
                table: "ViewingSchedules",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AgreedMonthlyRent",
                table: "Bookings",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ViewingId",
                table: "Bookings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Rooms_PropertyId_RoomTypeId_RoomId",
                table: "Rooms",
                columns: new[] { "PropertyId", "RoomTypeId", "RoomId" });

            migrationBuilder.CreateTable(
                name: "PropertyRoomTypes",
                columns: table => new
                {
                    PropertyId = table.Column<int>(type: "integer", nullable: false),
                    RoomTypeId = table.Column<int>(type: "integer", nullable: false),
                    MonthlyRent = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyRoomTypes", x => new { x.PropertyId, x.RoomTypeId });
                    table.CheckConstraint("CK_PropertyRoomTypes_MonthlyRent_Positive", "\"MonthlyRent\" > 0");
                    table.ForeignKey(
                        name: "FK_PropertyRoomTypes_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "PropertyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PropertyRoomTypes_RoomTypes_RoomTypeId",
                        column: x => x.RoomTypeId,
                        principalTable: "RoomTypes",
                        principalColumn: "RoomTypeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                INSERT INTO public."PropertyRoomTypes" ("PropertyId", "RoomTypeId", "MonthlyRent", "IsActive")
                SELECT DISTINCT
                    room."PropertyId",
                    room."RoomTypeId",
                    roomType."Price",
                    roomType."Price" >= 10000
                FROM public."Rooms" AS room
                INNER JOIN public."RoomTypes" AS roomType
                    ON roomType."RoomTypeId" = room."RoomTypeId"
                ON CONFLICT ("PropertyId", "RoomTypeId") DO NOTHING;

                UPDATE public."ViewingSchedules" AS viewing
                SET "PropertyId" = room."PropertyId",
                    "RoomTypeId" = room."RoomTypeId"
                FROM public."Rooms" AS room
                WHERE room."RoomId" = viewing."RoomId";

                DO $migration$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM public."ViewingSchedules"
                        WHERE "PropertyId" IS NULL OR "RoomTypeId" IS NULL
                    ) THEN
                        RAISE EXCEPTION 'ViewingSchedules backfill did not resolve every existing RoomId';
                    END IF;

                    ALTER TABLE public."PropertyRoomTypes" ENABLE ROW LEVEL SECURITY;

                    EXECUTE 'REVOKE ALL ON TABLE public."PropertyRoomTypes" FROM PUBLIC';
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'anon') THEN
                        EXECUTE 'REVOKE ALL ON TABLE public."PropertyRoomTypes" FROM anon';
                    END IF;
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'authenticated') THEN
                        EXECUTE 'REVOKE ALL ON TABLE public."PropertyRoomTypes" FROM authenticated';
                    END IF;
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'service_role') THEN
                        EXECUTE 'GRANT SELECT, INSERT, UPDATE ON TABLE public."PropertyRoomTypes" TO service_role';
                    END IF;
                END
                $migration$;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "PropertyId",
                table: "ViewingSchedules",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "RoomTypeId",
                table: "ViewingSchedules",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ViewingSchedules_PropertyId_RoomTypeId",
                table: "ViewingSchedules",
                columns: new[] { "PropertyId", "RoomTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_ViewingSchedules_PropertyId_RoomTypeId_RoomId",
                table: "ViewingSchedules",
                columns: new[] { "PropertyId", "RoomTypeId", "RoomId" });

            migrationBuilder.CreateIndex(
                name: "IX_ViewingSchedules_RoomTypeId",
                table: "ViewingSchedules",
                column: "RoomTypeId");

            migrationBuilder.CreateIndex(
                name: "UX_Bookings_ViewingId",
                table: "Bookings",
                column: "ViewingId",
                unique: true,
                filter: "\"ViewingId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bookings_AgreedMonthlyRent_Positive",
                table: "Bookings",
                sql: "\"AgreedMonthlyRent\" IS NULL OR \"AgreedMonthlyRent\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyRoomTypes_RoomTypeId_IsActive",
                table: "PropertyRoomTypes",
                columns: new[] { "RoomTypeId", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_ViewingSchedules_ViewingId",
                table: "Bookings",
                column: "ViewingId",
                principalTable: "ViewingSchedules",
                principalColumn: "ViewingId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Rooms_PropertyRoomTypes_PropertyId_RoomTypeId",
                table: "Rooms",
                columns: new[] { "PropertyId", "RoomTypeId" },
                principalTable: "PropertyRoomTypes",
                principalColumns: new[] { "PropertyId", "RoomTypeId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ViewingSchedules_Properties_PropertyId",
                table: "ViewingSchedules",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "PropertyId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ViewingSchedules_RoomTypes_RoomTypeId",
                table: "ViewingSchedules",
                column: "RoomTypeId",
                principalTable: "RoomTypes",
                principalColumn: "RoomTypeId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ViewingSchedules_Rooms_PropertyId_RoomTypeId_RoomId",
                table: "ViewingSchedules",
                columns: new[] { "PropertyId", "RoomTypeId", "RoomId" },
                principalTable: "Rooms",
                principalColumns: new[] { "PropertyId", "RoomTypeId", "RoomId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "This migration is forward-only after deployment. Use a verified database restore or a reviewed forward-fix; it is unsafe to discard property prices, selected rooms, or booking snapshots.");
        }
    }
}
