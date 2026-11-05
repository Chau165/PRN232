using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN232.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddViewingAssignmentsAndNotificationLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssignedTo",
                table: "ViewingSchedules",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManagerNote",
                table: "ViewingSchedules",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RespondedAt",
                table: "ViewingSchedules",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RespondedBy",
                table: "ViewingSchedules",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Body",
                table: "Notifications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RelatedEntityId",
                table: "Notifications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ViewingSchedules_ManagerNote_500",
                table: "ViewingSchedules",
                sql: "\"ManagerNote\" IS NULL OR char_length(\"ManagerNote\") <= 500");

            migrationBuilder.CreateIndex(
                name: "IX_ViewingSchedules_AssignedTo",
                table: "ViewingSchedules",
                column: "AssignedTo");

            migrationBuilder.CreateIndex(
                name: "IX_ViewingSchedules_RespondedBy",
                table: "ViewingSchedules",
                column: "RespondedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_ViewingSchedules_Users_AssignedTo",
                table: "ViewingSchedules",
                column: "AssignedTo",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ViewingSchedules_Users_RespondedBy",
                table: "ViewingSchedules",
                column: "RespondedBy",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ViewingSchedules_Users_AssignedTo",
                table: "ViewingSchedules");

            migrationBuilder.DropForeignKey(
                name: "FK_ViewingSchedules_Users_RespondedBy",
                table: "ViewingSchedules");

            migrationBuilder.DropIndex(
                name: "IX_ViewingSchedules_AssignedTo",
                table: "ViewingSchedules");

            migrationBuilder.DropIndex(
                name: "IX_ViewingSchedules_RespondedBy",
                table: "ViewingSchedules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ViewingSchedules_ManagerNote_500",
                table: "ViewingSchedules");

            migrationBuilder.DropColumn(
                name: "AssignedTo",
                table: "ViewingSchedules");

            migrationBuilder.DropColumn(
                name: "ManagerNote",
                table: "ViewingSchedules");

            migrationBuilder.DropColumn(
                name: "RespondedAt",
                table: "ViewingSchedules");

            migrationBuilder.DropColumn(
                name: "RespondedBy",
                table: "ViewingSchedules");

            migrationBuilder.DropColumn(
                name: "Body",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "RelatedEntityId",
                table: "Notifications");
        }
    }
}
