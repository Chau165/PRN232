using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN232.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingDepositTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rebuild 3.3: mọi booking đã có được tạo khi trang đặt cọc ghi "tiền cọc trừ vào tiền phòng tháng
            // đầu", nên giữ đúng lời hứa đó (true). Booking mới do code ghi false (cọc là tiền đảm bảo); model
            // không khai báo default nên EF luôn gửi giá trị tường minh khi thêm booking.
            migrationBuilder.AddColumn<bool>(
                name: "DepositAppliesToFirstMonth",
                table: "Bookings",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DepositAppliesToFirstMonth",
                table: "Bookings");
        }
    }
}
