using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN232.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomOccupancyFloorAndDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Rooms",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Floor",
                table: "Rooms",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxOccupants",
                table: "Rooms",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // Rebuild 2.1: phòng đã có nhận mức tối đa theo diện tích (8 m²/người, từ 1 đến 10 người),
            // giống RoomRules.MaxOccupantsFor. Phải điền TRƯỚC khi thêm ràng buộc bên dưới.
            migrationBuilder.Sql("""
                UPDATE "Rooms"
                SET "MaxOccupants" = GREATEST(1, LEAST(10, FLOOR("AreaSqm" / 8)::integer));
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Rooms_MaxOccupants_Range",
                table: "Rooms",
                sql: "\"MaxOccupants\" BETWEEN 1 AND 10");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Rooms_MaxOccupants_Range",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "Floor",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "MaxOccupants",
                table: "Rooms");
        }
    }
}
