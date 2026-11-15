using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PRN232.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContractOccupants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContractOccupants",
                columns: table => new
                {
                    ContractOccupantId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContractId = table.Column<int>(type: "integer", nullable: false),
                    FullName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IdentityNumber = table.Column<string>(type: "text", nullable: false),
                    MoveInDate = table.Column<DateOnly>(type: "date", nullable: false),
                    MoveOutDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AddedById = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractOccupants", x => x.ContractOccupantId);
                    table.CheckConstraint("CK_ContractOccupants_MoveOutAfterMoveIn", "\"MoveOutDate\" IS NULL OR \"MoveOutDate\" >= \"MoveInDate\"");
                    table.ForeignKey(
                        name: "FK_ContractOccupants_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "ContractId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractOccupants_Users_AddedById",
                        column: x => x.AddedById,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractOccupants_AddedById",
                table: "ContractOccupants",
                column: "AddedById");

            migrationBuilder.CreateIndex(
                name: "IX_ContractOccupants_ContractId",
                table: "ContractOccupants",
                column: "ContractId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractOccupants");
        }
    }
}
