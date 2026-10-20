using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PRN232.Infrastructure.Data;

#nullable disable

namespace PRN232.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261019034905_AddPropertyPrimaryImageUrl")]
public partial class AddPropertyPrimaryImageUrl : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE \"Properties\" ADD COLUMN IF NOT EXISTS \"PrimaryImageUrl\" text;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PrimaryImageUrl",
            table: "Properties");
    }
}
