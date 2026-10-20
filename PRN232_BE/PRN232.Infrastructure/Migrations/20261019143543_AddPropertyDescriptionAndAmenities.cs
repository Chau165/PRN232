using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN232.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertyDescriptionAndAmenities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Properties",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PropertyAmenities",
                columns: table => new
                {
                    PropertyId = table.Column<int>(type: "integer", nullable: false),
                    AmenityId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyAmenities", x => new { x.PropertyId, x.AmenityId });
                    table.ForeignKey(
                        name: "FK_PropertyAmenities_Amenities_AmenityId",
                        column: x => x.AmenityId,
                        principalTable: "Amenities",
                        principalColumn: "AmenityId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PropertyAmenities_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "PropertyId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyAmenities_AmenityId",
                table: "PropertyAmenities",
                column: "AmenityId");

            migrationBuilder.Sql("ALTER TABLE \"PropertyAmenities\" ENABLE ROW LEVEL SECURITY;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "PropertyAmenities")
                       OR EXISTS (SELECT 1 FROM "Properties" WHERE "Description" IS NOT NULL) THEN
                        RAISE EXCEPTION 'Cannot revert property description/amenities while the new fields contain data.';
                    END IF;
                END;
                $$;
                """);

            migrationBuilder.DropTable(
                name: "PropertyAmenities");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Properties");
        }
    }
}
