using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PRN232.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHandoverRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HandoverRecords",
                columns: table => new
                {
                    HandoverRecordId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContractId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    HandoverDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RecordedById = table.Column<int>(type: "integer", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HandoverRecords", x => x.HandoverRecordId);
                    table.CheckConstraint("CK_HandoverRecords_Kind", "\"Kind\" IN ('MoveIn', 'MoveOut')");
                    table.ForeignKey(
                        name: "FK_HandoverRecords_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "ContractId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HandoverRecords_Users_RecordedById",
                        column: x => x.RecordedById,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HandoverItems",
                columns: table => new
                {
                    HandoverItemId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HandoverRecordId = table.Column<int>(type: "integer", nullable: false),
                    EquipmentId = table.Column<int>(type: "integer", nullable: true),
                    EquipmentName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    CompensationValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Condition = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ChargeAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HandoverItems", x => x.HandoverItemId);
                    table.CheckConstraint("CK_HandoverItems_Charge", "\"ChargeAmount\" >= 0 AND \"ChargeAmount\" <= \"CompensationValue\" * \"Quantity\"");
                    table.CheckConstraint("CK_HandoverItems_Condition", "\"Condition\" IN ('Good', 'Worn', 'Damaged', 'Missing')");
                    table.CheckConstraint("CK_HandoverItems_Quantity_Range", "\"Quantity\" BETWEEN 1 AND 20");
                    table.ForeignKey(
                        name: "FK_HandoverItems_Equipments_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipments",
                        principalColumn: "EquipmentId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HandoverItems_HandoverRecords_HandoverRecordId",
                        column: x => x.HandoverRecordId,
                        principalTable: "HandoverRecords",
                        principalColumn: "HandoverRecordId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HandoverItems_EquipmentId",
                table: "HandoverItems",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_HandoverItems_HandoverRecordId",
                table: "HandoverItems",
                column: "HandoverRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_HandoverRecords_RecordedById",
                table: "HandoverRecords",
                column: "RecordedById");

            migrationBuilder.CreateIndex(
                name: "UX_HandoverRecords_ContractId_Kind",
                table: "HandoverRecords",
                columns: new[] { "ContractId", "Kind" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HandoverItems");

            migrationBuilder.DropTable(
                name: "HandoverRecords");
        }
    }
}
