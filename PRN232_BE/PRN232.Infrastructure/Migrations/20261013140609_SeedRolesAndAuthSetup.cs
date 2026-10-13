using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PRN232.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedRolesAndAuthSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent thay vì InsertData thường: trên DB đã có sẵn 3 role này rồi
            // (ai đó add tay qua Table Editor trước khi module Auth tồn tại) — InsertData
            // thường sẽ báo "duplicate key" trên DB đã có data, ON CONFLICT DO NOTHING thì
            // chạy được trên cả DB mới (insert) lẫn DB đã có sẵn data y hệt (no-op).
            migrationBuilder.Sql(@"
                INSERT INTO ""Roles"" (""RoleId"", ""RoleName"") VALUES
                    (1, 'Customer'),
                    (2, 'Manager'),
                    (3, 'Admin')
                ON CONFLICT (""RoleId"") DO NOTHING;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.Sql(@"DELETE FROM ""Roles"" WHERE ""RoleId"" IN (1, 2, 3);");
        }
    }
}
