using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PRN232.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingApplicationReviewWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RosterChangeNote",
                table: "BookingApplications",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RosterChangeStatus",
                table: "BookingApplications",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<DateTime>(
                name: "RosterRequestedAtUtc",
                table: "BookingApplications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RosterReviewedAtUtc",
                table: "BookingApplications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RosterReviewedById",
                table: "BookingApplications",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BookingApplicationReviews",
                columns: table => new
                {
                    BookingApplicationReviewId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BookingApplicationId = table.Column<int>(type: "integer", nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: true),
                    PersonRevision = table.Column<int>(type: "integer", nullable: true),
                    Kind = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Decision = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReviewerId = table.Column<int>(type: "integer", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpectedApplicationVersion = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingApplicationReviews", x => x.BookingApplicationReviewId);
                    table.CheckConstraint("CK_BookingApplicationReviews_Decision", "\"Decision\" IN ('Approved','Rejected') AND \"Kind\" IN ('Identity','Roster')");
                    table.CheckConstraint("CK_BookingApplicationReviews_Note", "\"Decision\" <> 'Rejected' OR (\"Note\" IS NOT NULL AND length(trim(\"Note\")) > 0)");
                    table.ForeignKey(
                        name: "FK_BookingApplicationReviews_BookingApplications_BookingApplic~",
                        column: x => x.BookingApplicationId,
                        principalTable: "BookingApplications",
                        principalColumn: "BookingApplicationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookingApplicationReviews_Users_ReviewerId",
                        column: x => x.ReviewerId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingApplications_RosterReviewedById",
                table: "BookingApplications",
                column: "RosterReviewedById");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BookingApplications_Roster",
                table: "BookingApplications",
                sql: "\"RosterChangeStatus\" IN ('None','Pending','Approved','Rejected')");

            migrationBuilder.CreateIndex(
                name: "IX_BookingApplicationReviews_BookingApplicationId",
                table: "BookingApplicationReviews",
                column: "BookingApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingApplicationReviews_ReviewerId",
                table: "BookingApplicationReviews",
                column: "ReviewerId");

            migrationBuilder.AddForeignKey(
                name: "FK_BookingApplications_Users_RosterReviewedById",
                table: "BookingApplications",
                column: "RosterReviewedById",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
                ALTER TABLE public."BookingApplicationReviews" ENABLE ROW LEVEL SECURITY;
                REVOKE ALL PRIVILEGES ON TABLE public."BookingApplicationReviews" FROM PUBLIC;
                DO $review_security$
                DECLARE api_role text;
                BEGIN
                    FOREACH api_role IN ARRAY ARRAY['anon','authenticated','service_role'] LOOP
                        IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname=api_role) THEN
                            EXECUTE format('REVOKE ALL PRIVILEGES ON TABLE public.%I FROM %I','BookingApplicationReviews',api_role);
                        END IF;
                    END LOOP;
                END;
                $review_security$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BookingApplications_Users_RosterReviewedById",
                table: "BookingApplications");

            migrationBuilder.DropTable(
                name: "BookingApplicationReviews");

            migrationBuilder.DropIndex(
                name: "IX_BookingApplications_RosterReviewedById",
                table: "BookingApplications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BookingApplications_Roster",
                table: "BookingApplications");

            migrationBuilder.DropColumn(
                name: "RosterChangeNote",
                table: "BookingApplications");

            migrationBuilder.DropColumn(
                name: "RosterChangeStatus",
                table: "BookingApplications");

            migrationBuilder.DropColumn(
                name: "RosterRequestedAtUtc",
                table: "BookingApplications");

            migrationBuilder.DropColumn(
                name: "RosterReviewedAtUtc",
                table: "BookingApplications");

            migrationBuilder.DropColumn(
                name: "RosterReviewedById",
                table: "BookingApplications");
        }
    }
}
