# Phase 3.1A — Tóm tắt điều hành

**Trạng thái:** COMPLETE — LIVE VERIFIED; BILLING IMPLEMENTATION GAP; OWNER APPROVAL REQUIRED
**Snapshot PostgreSQL:** 2026-10-19 05:18:04 UTC
**Phạm vi:** Audit chỉ đọc Supabase, đối chiếu source BE/FE và migration, đề xuất thiết kế Property Detail. Không triển khai Phase 3.1B.

## Kết luận

Đã kết nối bằng cấu hình DefaultConnection hiện có của BE. Project ref được đối chiếu; endpoint cấu hình là Supabase pooler trên cổng 5432, database postgres, PostgreSQL 17.11. Mỗi phiên truy vấn audit chạy trong REPEATABLE READ READ ONLY và kết thúc bằng ROLLBACK. Giá trị kết nối và secrets được giữ kín.

| Khu vực Property Detail | Kết quả đã xác minh | Vì sao chưa đầy đủ |
|---|---|---|
| Giới thiệu | Properties không có Description; Entity, DTO và endpoint public cũng không có trường này. FE hiện hiển thị trạng thái đang cập nhật. | Thiếu dữ liệu và đường ghi/đọc được phê duyệt. |
| Tiện ích chung | Không có PropertyAmenities hay quan hệ property-level tương đương. API không có commonAmenities; FE đang hiển thị placeholder. | RoomAmenities chỉ chứng minh tiện ích gắn với từng phòng, không chứng minh tiện ích chung. |
| Tiện ích trong phòng | Có 27 liên kết RoomAmenities; không phát hiện orphan hoặc trùng. Source public detail tổng hợp tiện ích trên tập phòng rentable và trả số phòng được gắn mỗi tiện ích. | API runtime localhost:5102 vẫn chạy hợp đồng cũ và Swagger không có roomAmenities, dù source hiện tại đã có. |
| Chi phí sinh hoạt | Có 4 Services và 12 RoomTypeServices; mỗi dịch vụ có một Services.Price dùng chung. Không có đơn vị/billing basis có cấu trúc hoặc giá theo Property. | Không thể khẳng định giá trong catalog là giá niêm yết Property hay đơn giá Invoice. Endpoint Property Detail và FE chưa có livingCosts. |

Theo predicate đang cài trong RoomRentabilityPolicy, ba Property mục tiêu có 1, 2, 1 phòng rentable. Đây là snapshot theo source hiện tại, không phải phê duyệt nghiệp vụ mới; các báo cáo trước vẫn để một số ngữ nghĩa rentability chờ owner xác nhận.

## Billing và migration gate

BE có entity, DbSet và schema cho Invoices, InvoiceDetails, UtilityReadings, Payments; audit source không tìm thấy Invoice/Billing controller, service, repository hoặc logic tạo hóa đơn. InvoiceDetails chỉ lưu ItemType, Description, Amount; UtilityReadings lưu số công tơ, không lưu rate. FE có màn hình hóa đơn mô phỏng bằng giá hardcode và localStorage; các giá này không khớp hoàn toàn Services.Price live.

Migration 20261013192052_ReconcileMaintenanceReportsAndLogNullability có trong source nhưng không có trong history live. Hai bảng mà migration định tạo đã tồn tại và có dữ liệu; một số index/default cũng lệch migration. Chạy database update hoặc rollback migration này một cách mù quáng có thể lỗi hoặc làm mất dữ liệu. Phải xử lý migration reconciliation trước mọi migration mới.

## Khuyến nghị để owner review

1. Thêm Description nullable vào Properties; chỉ lưu nội dung đã được chủ khu trọ xác nhận.
2. Tách tiện ích chung khỏi tiện ích phòng: giữ roomAmenities là dữ liệu suy ra từ phòng rentable; nếu cần danh sách chung do Manager xác nhận, thêm PropertyAmenities và CRUD có scope.
3. Nếu rate khác nhau theo khu trọ, chọn mô hình rate theo Property + Service, có UnitCode/BillingBasis và ngày hiệu lực. Giữ Services.Price tương thích tạm thời nhưng không dùng làm nguồn rate mới sau cutover.
4. Khi phát hành Invoice, snapshot rate, unit, quantity và amount vào dòng hóa đơn; thay đổi rate sau đó không được viết lại hóa đơn đã phát hành.
5. Giải quyết migration reconciliation live trước Phase 3.1B.

Mức tin cậy cao cho schema, dữ liệu aggregate và hợp đồng Swagger runtime tại snapshot; trung bình cho phân tích call path do chỉ audit module liên quan. Không chạy test/build trong audit này. Báo cáo Phase 3.1 trước đó có ghi một số PostgreSQL test còn kỳ vọng PrimaryImageUrl null; thông tin đó không được chạy lại ở phase này.

## Bằng chứng và giới hạn

- Program.cs lấy ConnectionStrings:DefaultConnection; process environment override tồn tại và đã dùng, User Secrets cũng có khóa cùng tên. Không ghi giá trị.
- SQL audit xác nhận transaction_read_only=on; chỉ đọc aggregate/catalog; không truy vấn PII.
- Vai trò DB hiện tại không phải superuser nhưng có BYPASSRLS; kết quả SQL không chứng minh quyền anon/authenticated hay quyền của API client.
- Đã dùng CodeGraph cho luồng Services/RoomTypeServices. Các file public-listing chưa được index là untracked nên được kiểm tra trực tiếp; FE không có CodeGraph index. Đây là trace theo phạm vi liên quan, không phải rà toàn repository.
- Không sửa source/FE, không ghi DB/Storage, không chạy migration, không restart/stop tiến trình, không commit/push. Worktree đã dirty trước audit; chỉ tạo báo cáo trong thư mục này.

## Artifact còn lại

02 Live schema/data; 03 Amenities; 04 Services/pricing/Billing; 05 BE/FE/API gap matrix; 06 Design options; 07 Owner decisions; 08 Phase 3.1B plan; 09 Read-only SQL.
