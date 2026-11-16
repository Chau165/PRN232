using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PRN232.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingIdentityPreparation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BookingApplications",
                columns: table => new
                {
                    BookingApplicationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    RoomId = table.Column<int>(type: "integer", nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MoveInDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OccupantCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingApplications", x => x.BookingApplicationId);
                    table.CheckConstraint("CK_BookingApplications_Count", "\"OccupantCount\" >= 1");
                    table.CheckConstraint("CK_BookingApplications_Status", "\"Status\" IN ('Draft','Submitted','Cancelled','Converted')");
                    table.ForeignKey(
                        name: "FK_BookingApplications_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "BookingId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookingApplications_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "RoomId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookingApplications_Users_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BookingApplicationPeople",
                columns: table => new
                {
                    BookingApplicationPersonId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BookingApplicationId = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    FullName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IdentityNumber = table.Column<string>(type: "text", nullable: false),
                    IdentityNumberHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    ReviewedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ConsentRecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingApplicationPeople", x => x.BookingApplicationPersonId);
                    table.CheckConstraint("CK_BookingApplicationPeople_Position", "\"Position\" >= 0 AND \"Revision\" >= 1");
                    table.CheckConstraint("CK_BookingApplicationPeople_Rejection", "\"Status\" <> 'Rejected' OR length(trim(\"ReviewNote\")) > 0 AND \"ReviewNote\" IS NOT NULL");
                    table.CheckConstraint("CK_BookingApplicationPeople_Review", "\"Status\" NOT IN ('Approved','Rejected') OR (\"ReviewedByUserId\" IS NOT NULL AND \"ReviewedAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_BookingApplicationPeople_Status", "\"Status\" IN ('Incomplete','PendingReview','Approved','Rejected')");
                    table.ForeignKey(
                        name: "FK_BookingApplicationPeople_BookingApplications_BookingApplica~",
                        column: x => x.BookingApplicationId,
                        principalTable: "BookingApplications",
                        principalColumn: "BookingApplicationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookingApplicationPeople_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BookingIdentityImages",
                columns: table => new
                {
                    BookingIdentityImageId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BookingApplicationPersonId = table.Column<int>(type: "integer", nullable: false),
                    Side = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    ObjectPath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    PersonRevision = table.Column<int>(type: "integer", nullable: false),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingIdentityImages", x => x.BookingIdentityImageId);
                    table.CheckConstraint("CK_BookingIdentityImages_Side", "\"Side\" IN ('Front','Back')");
                    table.CheckConstraint("CK_BookingIdentityImages_Size", "\"SizeBytes\" > 0 AND \"SizeBytes\" <= 5242880 AND \"PersonRevision\" >= 1");
                    table.CheckConstraint("CK_BookingIdentityImages_Type", "\"ContentType\" IN ('image/jpeg','image/png','image/webp')");
                    table.ForeignKey(
                        name: "FK_BookingIdentityImages_BookingApplicationPeople_BookingAppli~",
                        column: x => x.BookingApplicationPersonId,
                        principalTable: "BookingApplicationPeople",
                        principalColumn: "BookingApplicationPersonId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingApplicationPeople_BookingApplicationId_Position",
                table: "BookingApplicationPeople",
                columns: new[] { "BookingApplicationId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingApplicationPeople_ReviewedByUserId",
                table: "BookingApplicationPeople",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingApplications_BookingId",
                table: "BookingApplications",
                column: "BookingId",
                unique: true,
                filter: "\"BookingId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BookingApplications_CustomerId",
                table: "BookingApplications",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingApplications_RoomId",
                table: "BookingApplications",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingIdentityImages_BookingApplicationPersonId_Side",
                table: "BookingIdentityImages",
                columns: new[] { "BookingApplicationPersonId", "Side" },
                unique: true,
                filter: "\"IsCurrent\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_BookingIdentityImages_ObjectPath",
                table: "BookingIdentityImages",
                column: "ObjectPath",
                unique: true);
            foreach (var table in new[] { "BookingApplications", "BookingApplicationPeople", "BookingIdentityImages" })
            {
                migrationBuilder.Sql($"""
                    ALTER TABLE public."{table}" ENABLE ROW LEVEL SECURITY;
                    REVOKE ALL PRIVILEGES ON TABLE public."{table}" FROM PUBLIC;
                    DO $identity_security$
                    DECLARE api_role text;
                    BEGIN
                        FOREACH api_role IN ARRAY ARRAY['anon', 'authenticated', 'service_role'] LOOP
                            IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = api_role) THEN
                                EXECUTE format('REVOKE ALL PRIVILEGES ON TABLE public.%I FROM %I', '{table}', api_role);
                            END IF;
                        END LOOP;
                    END;
                    $identity_security$;
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingIdentityImages");

            migrationBuilder.DropTable(
                name: "BookingApplicationPeople");

            migrationBuilder.DropTable(
                name: "BookingApplications");
        }
    }
}
