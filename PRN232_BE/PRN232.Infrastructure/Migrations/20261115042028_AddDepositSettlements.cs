using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PRN232.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDepositSettlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DepositSettlements",
                columns: table => new
                {
                    DepositSettlementId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContractId = table.Column<int>(type: "integer", nullable: false),
                    MoveOutDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DepositAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DamageCharges = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OutstandingDebt = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RetainedForDebt = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ForfeitedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RefundAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AmountDue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EarlyWithoutNotice = table.Column<bool>(type: "boolean", nullable: false),
                    NoticeDays = table.Column<int>(type: "integer", nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SettledById = table.Column<int>(type: "integer", nullable: false),
                    SettledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RefundConfirmedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RefundConfirmedById = table.Column<int>(type: "integer", nullable: true),
                    DueCollectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueCollectedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepositSettlements", x => x.DepositSettlementId);
                    table.CheckConstraint("CK_DepositSettlements_Balanced", "\"DamageCharges\" - \"AmountDue\" + \"RetainedForDebt\" + \"ForfeitedAmount\" + \"RefundAmount\" = \"DepositAmount\"");
                    table.CheckConstraint("CK_DepositSettlements_NonNegative", "\"DepositAmount\" >= 0 AND \"DamageCharges\" >= 0 AND \"OutstandingDebt\" >= 0 AND \"RetainedForDebt\" >= 0 AND \"ForfeitedAmount\" >= 0 AND \"RefundAmount\" >= 0 AND \"AmountDue\" >= 0");
                    table.ForeignKey(
                        name: "FK_DepositSettlements_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "ContractId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepositSettlements_Users_DueCollectedById",
                        column: x => x.DueCollectedById,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepositSettlements_Users_RefundConfirmedById",
                        column: x => x.RefundConfirmedById,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepositSettlements_Users_SettledById",
                        column: x => x.SettledById,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DepositSettlements_DueCollectedById",
                table: "DepositSettlements",
                column: "DueCollectedById");

            migrationBuilder.CreateIndex(
                name: "IX_DepositSettlements_RefundConfirmedById",
                table: "DepositSettlements",
                column: "RefundConfirmedById");

            migrationBuilder.CreateIndex(
                name: "IX_DepositSettlements_SettledById",
                table: "DepositSettlements",
                column: "SettledById");

            migrationBuilder.CreateIndex(
                name: "UX_DepositSettlements_ContractId",
                table: "DepositSettlements",
                column: "ContractId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DepositSettlements");
        }
    }
}
