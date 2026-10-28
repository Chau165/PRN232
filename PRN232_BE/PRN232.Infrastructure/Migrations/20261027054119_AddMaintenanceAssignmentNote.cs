using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN232.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceAssignmentNote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignmentNote",
                table: "MaintenanceRequests",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dropping this column permanently deletes all saved assignment notes.
            migrationBuilder.DropColumn(
                name: "AssignmentNote",
                table: "MaintenanceRequests");
        }
    }
}
