using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN232.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipmentQuantityAndCompensationValue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CompensationValue",
                table: "Equipments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                table: "Equipments",
                type: "integer",
                nullable: false,
                // Rebuild 2.2: thiết bị cũ mỗi dòng là 1 cái. Phải là 1 (không phải 0 như EF sinh ra),
                // nếu không ràng buộc "Quantity BETWEEN 1 AND 20" bên dưới sẽ làm migration thất bại.
                // CompensationValue = 0 nghĩa là chưa định giá; Admin cập nhật dần.
                defaultValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Equipments_CompensationValue_NonNegative",
                table: "Equipments",
                sql: "\"CompensationValue\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Equipments_Quantity_Range",
                table: "Equipments",
                sql: "\"Quantity\" BETWEEN 1 AND 20");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Equipments_CompensationValue_NonNegative",
                table: "Equipments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Equipments_Quantity_Range",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "CompensationValue",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "Equipments");
        }
    }
}
