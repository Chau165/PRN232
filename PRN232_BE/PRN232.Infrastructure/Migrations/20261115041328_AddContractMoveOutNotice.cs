using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN232.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContractMoveOutNotice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "MoveOutNoticeAtUtc",
                table: "Contracts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MoveOutNoticeById",
                table: "Contracts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PlannedMoveOutDate",
                table: "Contracts",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_MoveOutNoticeById",
                table: "Contracts",
                column: "MoveOutNoticeById");

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_Users_MoveOutNoticeById",
                table: "Contracts",
                column: "MoveOutNoticeById",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_Users_MoveOutNoticeById",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_MoveOutNoticeById",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "MoveOutNoticeAtUtc",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "MoveOutNoticeById",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "PlannedMoveOutDate",
                table: "Contracts");
        }
    }
}
