using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PRN232.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContractTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TransferredFromContractId",
                table: "Contracts",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContractTransfers",
                columns: table => new
                {
                    ContractTransferId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FromContractId = table.Column<int>(type: "integer", nullable: false),
                    NewHolderId = table.Column<int>(type: "integer", nullable: false),
                    BecomingHolderOccupantId = table.Column<int>(type: "integer", nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequestedById = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OldHolderConfirmedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NewHolderConfirmedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedById = table.Column<int>(type: "integer", nullable: true),
                    NewContractId = table.Column<int>(type: "integer", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledById = table.Column<int>(type: "integer", nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractTransfers", x => x.ContractTransferId);
                    table.CheckConstraint("CK_ContractTransfers_Status", "\"Status\" IN ('Pending', 'Completed', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_ContractTransfers_Contracts_FromContractId",
                        column: x => x.FromContractId,
                        principalTable: "Contracts",
                        principalColumn: "ContractId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractTransfers_Contracts_NewContractId",
                        column: x => x.NewContractId,
                        principalTable: "Contracts",
                        principalColumn: "ContractId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractTransfers_Users_NewHolderId",
                        column: x => x.NewHolderId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractTransfers_Users_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Contracts_TransferredFromContractId",
                table: "Contracts",
                column: "TransferredFromContractId",
                unique: true,
                filter: "\"TransferredFromContractId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContractTransfers_NewContractId",
                table: "ContractTransfers",
                column: "NewContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractTransfers_NewHolderId",
                table: "ContractTransfers",
                column: "NewHolderId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractTransfers_RequestedById",
                table: "ContractTransfers",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "UX_ContractTransfers_PendingPerContract",
                table: "ContractTransfers",
                column: "FromContractId",
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_Contracts_TransferredFromContractId",
                table: "Contracts",
                column: "TransferredFromContractId",
                principalTable: "Contracts",
                principalColumn: "ContractId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_Contracts_TransferredFromContractId",
                table: "Contracts");

            migrationBuilder.DropTable(
                name: "ContractTransfers");

            migrationBuilder.DropIndex(
                name: "UX_Contracts_TransferredFromContractId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "TransferredFromContractId",
                table: "Contracts");
        }
    }
}
