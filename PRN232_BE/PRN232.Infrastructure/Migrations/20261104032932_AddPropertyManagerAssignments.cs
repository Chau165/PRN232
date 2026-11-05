using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN232.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertyManagerAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PropertyManagerAssignments",
                columns: table => new
                {
                    PropertyId = table.Column<int>(type: "integer", nullable: false),
                    ManagerId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_PropertyManagerAssignments",
                        assignment => new { assignment.PropertyId, assignment.ManagerId });
                    table.ForeignKey(
                        name: "FK_PropertyManagerAssignments_Properties_PropertyId",
                        column: assignment => assignment.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "PropertyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PropertyManagerAssignments_Users_ManagerId",
                        column: assignment => assignment.ManagerId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyManagerAssignments_ManagerId",
                table: "PropertyManagerAssignments",
                column: "ManagerId");

            migrationBuilder.Sql("ALTER TABLE \"PropertyManagerAssignments\" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("REVOKE ALL PRIVILEGES ON TABLE \"PropertyManagerAssignments\" FROM PUBLIC, anon, authenticated, service_role;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PropertyManagerAssignments");
        }
    }
}
