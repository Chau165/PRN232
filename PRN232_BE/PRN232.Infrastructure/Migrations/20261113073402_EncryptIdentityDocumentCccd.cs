using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PRN232.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EncryptIdentityDocumentCccd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A9. Existing plaintext CCCD numbers are encrypted (and hashed) by
            // IdentityDocumentEncryptionBackfill at app startup, because SQL here cannot reach the
            // Data Protection keys.
            migrationBuilder.AddColumn<string>(
                name: "CCCDNumberHash",
                table: "IdentityDocuments",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdentityDocuments_CCCDNumberHash",
                table: "IdentityDocuments",
                column: "CCCDNumberHash");

            // DataProtectionKeys holds the keys that decrypt CCCD numbers: never expose it through
            // Supabase's REST API (same convention as every other table in this schema).
            migrationBuilder.Sql("ALTER TABLE \"DataProtectionKeys\" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("REVOKE ALL PRIVILEGES ON TABLE \"DataProtectionKeys\" FROM PUBLIC, anon, authenticated, service_role;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // WARNING: dropping DataProtectionKeys makes every encrypted CCCDNumber unreadable.
            // Back up that table (or decrypt the data first) before rolling this migration back.
            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropIndex(
                name: "IX_IdentityDocuments_CCCDNumberHash",
                table: "IdentityDocuments");

            migrationBuilder.DropColumn(
                name: "CCCDNumberHash",
                table: "IdentityDocuments");
        }
    }
}
