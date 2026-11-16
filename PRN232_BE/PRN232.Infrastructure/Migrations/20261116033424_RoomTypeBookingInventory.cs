using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN232.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RoomTypeBookingInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Rooms_RoomId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_RoomId",
                table: "Bookings");

            migrationBuilder.AlterColumn<int>(
                name: "RoomId",
                table: "Bookings",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "PropertyId",
                table: "Bookings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RoomTypeId",
                table: "Bookings",
                type: "integer",
                nullable: true);

            // Location only: retain physical room, all financial snapshots and paid history exactly as stored.
            migrationBuilder.Sql("""
                UPDATE "Bookings" AS booking
                SET "PropertyId" = room."PropertyId", "RoomTypeId" = room."RoomTypeId"
                FROM "Rooms" AS room WHERE booking."RoomId" = room."RoomId";
                """);
            migrationBuilder.AlterColumn<int>(name: "PropertyId", table: "Bookings", type: "integer",
                nullable: false, oldClrType: typeof(int), oldType: "integer", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "RoomTypeId", table: "Bookings", type: "integer",
                nullable: false, oldClrType: typeof(int), oldType: "integer", oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_PropertyId_RoomTypeId_RoomId",
                table: "Bookings",
                columns: new[] { "PropertyId", "RoomTypeId", "RoomId" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_PropertyId_RoomTypeId_Status_ExpiryDate",
                table: "Bookings",
                columns: new[] { "PropertyId", "RoomTypeId", "Status", "ExpiryDate" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bookings_Location_Positive",
                table: "Bookings",
                sql: "\"PropertyId\" > 0 AND \"RoomTypeId\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bookings_Unassigned_Terms",
                table: "Bookings",
                sql: "\"RoomId\" IS NOT NULL OR (\"AgreedMonthlyRent\" IS NOT NULL AND \"AgreedMonthlyRent\" > 0 AND \"DepositAmount\" > 0 AND \"OccupantCount\" IS NOT NULL AND \"MoveInDate\" IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Properties_PropertyId",
                table: "Bookings",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "PropertyId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_PropertyRoomTypes_PropertyId_RoomTypeId",
                table: "Bookings",
                columns: new[] { "PropertyId", "RoomTypeId" },
                principalTable: "PropertyRoomTypes",
                principalColumns: new[] { "PropertyId", "RoomTypeId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Rooms_PropertyId_RoomTypeId_RoomId",
                table: "Bookings",
                columns: new[] { "PropertyId", "RoomTypeId", "RoomId" },
                principalTable: "Rooms",
                principalColumns: new[] { "PropertyId", "RoomTypeId", "RoomId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Downgrade must not invent a physical room or discard unallocated reservations.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "Bookings" WHERE "RoomId" IS NULL) THEN
                        RAISE EXCEPTION 'Cannot downgrade while unassigned room-type bookings exist; resolve them explicitly first.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Properties_PropertyId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_PropertyRoomTypes_PropertyId_RoomTypeId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Rooms_PropertyId_RoomTypeId_RoomId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_PropertyId_RoomTypeId_RoomId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_PropertyId_RoomTypeId_Status_ExpiryDate",
                table: "Bookings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Bookings_Location_Positive",
                table: "Bookings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Bookings_Unassigned_Terms",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "PropertyId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "RoomTypeId",
                table: "Bookings");

            migrationBuilder.AlterColumn<int>(
                name: "RoomId",
                table: "Bookings",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_RoomId",
                table: "Bookings",
                column: "RoomId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Rooms_RoomId",
                table: "Bookings",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "RoomId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
