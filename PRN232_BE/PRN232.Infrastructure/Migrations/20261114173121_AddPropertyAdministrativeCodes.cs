using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN232.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertyAdministrativeCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProvinceCode",
                table: "Properties",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WardCode",
                table: "Properties",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Rebuild 1.4: gán mã sau sáp nhập cho khu trọ đã có. Đối chiếu ngày 15/11/2026 trên toàn bộ
            // 32 khu trọ của DB Live (docs/rebuild/PLAN.md, D1): đều thuộc TP.HCM, cột Ward đã là tên phường
            // mới (thiếu chữ "Phường"/"Xã"), trừ hai trường hợp ghi riêng bên dưới. Khớp theo TÊN chứ không
            // theo PropertyId để chạy an toàn trên DB khác; dòng nào không khớp giữ mã rỗng và được gán khi
            // Admin chọn lại tỉnh/phường. Cột District giữ nguyên, sẽ bỏ ở migration riêng.
            migrationBuilder.Sql("""
                UPDATE "Properties" AS p
                SET "ProvinceCode" = '79', "WardCode" = m.code, "Ward" = m.official
                FROM (VALUES
                    ('26740', 'Phường Sài Gòn', 'Sài Gòn'),
                    ('26743', 'Phường Bến Thành', 'Bến Thành'),
                    ('26773', 'Phường Thới An', 'Thới An'),
                    ('26791', 'Phường Đông Hưng Thuận', 'Đông Hưng Thuận'),
                    ('26809', 'Phường Hiệp Bình', 'Hiệp Bình'),
                    ('26878', 'Phường An Hội Đông', 'An Hội Đông'),
                    ('26890', 'Phường Hạnh Thông', 'Hạnh Thông'),
                    ('26944', 'Phường Gia Định', 'Gia Định'),
                    ('26983', 'Phường Bảy Hiền', 'Bảy Hiền'),
                    ('27013', 'Phường Tây Thạnh', 'Tây Thạnh'),
                    ('27058', 'Phường Cầu Kiệu', 'Cầu Kiệu'),
                    ('27073', 'Phường Phú Nhuận', 'Phú Nhuận'),
                    ('27094', 'Phường An Khánh', 'An Khánh'),
                    ('27139', 'Phường Xuân Hòa', 'Xuân Hòa'),
                    ('27154', 'Phường Bàn Cờ', 'Bàn Cờ'),
                    ('27169', 'Phường Diên Hồng', 'Diên Hồng'),
                    ('27190', 'Phường Vườn Lài', 'Vườn Lài'),
                    ('27232', 'Phường Bình Thới', 'Bình Thới'),
                    ('27259', 'Phường Xóm Chiếu', 'Xóm Chiếu'),
                    ('27343', 'Phường Chợ Lớn', 'Chợ Lớn'),
                    ('27367', 'Phường Bình Tây', 'Bình Tây'),
                    ('27418', 'Phường Chánh Hưng', 'Chánh Hưng'),
                    ('27424', 'Phường Bình Đông', 'Bình Đông'),
                    ('27460', 'Phường An Lạc', 'An Lạc'),
                    ('27475', 'Phường Tân Hưng', 'Tân Hưng'),
                    ('27478', 'Phường Tân Thuận', 'Tân Thuận'),
                    ('27592', 'Xã Bà Điểm', 'Bà Điểm'),
                    ('27619', 'Xã Bình Hưng', 'Bình Hưng'),
                    ('27655', 'Xã Nhà Bè', 'Nhà Bè'),
                    -- Phường Linh Trung cũ được nhập toàn bộ vào Phường Linh Xuân (NQ 1685/NQ-UBTVQH15).
                    ('26800', 'Phường Linh Xuân', 'Linh Trung')
                ) AS m(code, official, short_name)
                WHERE p."WardCode" = ''
                  AND p."City" = 'Thành phố Hồ Chí Minh'
                  AND btrim(p."Ward") IN (m.official, m.short_name);

                -- Khu trọ nhập nhầm tên phường vào cột District, để trống cột Ward.
                UPDATE "Properties"
                SET "ProvinceCode" = '79', "WardCode" = '26743', "Ward" = 'Phường Bến Thành'
                WHERE "WardCode" = ''
                  AND "City" = 'Thành phố Hồ Chí Minh'
                  AND btrim("Ward") = ''
                  AND btrim("District") = 'Bến Thành';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProvinceCode",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "WardCode",
                table: "Properties");
        }
    }
}
