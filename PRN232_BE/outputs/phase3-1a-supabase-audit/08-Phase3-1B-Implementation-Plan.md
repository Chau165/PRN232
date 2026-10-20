# Phase 3.1A — Phase 3.1B Implementation Plan

Kế hoạch đề xuất, chưa triển khai. Bắt đầu sau khi các quyết định BLOCKER ở báo cáo 07 được xác nhận. Không seed nghiệp vụ chưa được duyệt.

## Trình tự và ownership

| Bước | Công việc | Owner | Gate |
|---|---|---|---|
| 0. Chốt nghiệp vụ | Chọn rate scope A/B/C; unit/basis; vai trò RoomTypeServices; invoice snapshot; common amenities; behavior Property hết phòng; dữ liệu backfill. | Chủ dự án + Billing + BE/DB | Có quyết định được ghi nhận. Nếu thiếu rate scope, unit hoặc snapshot, dừng phần rates/Billing. |
| 1. Reconcile migration drift | So khớp live schema, EF migrations, snapshot và __EFMigrationsHistory cho MaintenanceReports/MaintenanceReportImages. Migration pending tạo bảng đã có trên live; index/default còn lệch. Thiết kế remediation forward-only được review. | DB/BE | Xác nhận migration không tạo lại bảng, drop data hay làm sai history. Release gate trước mọi EF database update. |
| 2. DB/EF migration additive | Theo lựa chọn đã duyệt: Description nullable; PropertyAmenities với PK/FK; rate schema/metadata theo scope và hiệu lực. Cập nhật entity, EF config, snapshot và migration đồng bộ. | DB/BE | Review migration/constraints/index; API cũ còn chạy với schema mở rộng. |
| 3. Backfill có kiểm soát | Nhập Description, common amenities, unit/rates chỉ từ owner/Billing. Có SELECT preview/count, phạm vi giới hạn, hậu kiểm và script rerun an toàn. Không suy ra common amenity từ RoomAmenities, không lấy unit từ tên Service, không sửa invoice lịch sử. | Owner cung cấp; DB/BE thực hiện | Số lượng trước/sau khớp duyệt; rerun không nhân đôi; không log PII/secret. |
| 4. BE CRUD và public GET | Thêm CRUD có authorization và Property scope. Mở rộng GET /api/public/properties/{propertyId} theo hướng additive: description nullable, commonAmenities [], roomAmenities [], livingCosts [] chỉ với rate public hợp lệ. Giữ policy thuê phòng; không trả owner/tenant/booking/contract detail. | BE | Unit/integration checks: projection, authorization/scope, null/empty, 404, không lộ dữ liệu. Rate có unit/basis đã duyệt. |
| 5. Billing/snapshot nếu được duyệt | Tính theo kỳ, quantity, rounding và rate đã duyệt; ghi snapshot trên invoice line khi phát hành. Rate mới không đổi invoice đã phát hành. | Billing + BE | Kiểm tra từng basis và kỳ; xác nhận invoice cũ không thay đổi. Nếu Billing chưa làm, giữ trạng thái NOT IMPLEMENTED. |
| 6. FE mapping | Render theo thứ tự Giới thiệu → Tiện ích chung → Tiện ích trong phòng → Chi phí. Có trạng thái null/empty; đơn vị lấy từ API. Không hardcode giá mock hoặc tự thêm “đ/kWh”. | FE | Kiểm tra dữ liệu có/không và Property hết phòng theo quyết định. |
| 7. Runtime/release | Deploy có chủ đích; xác minh process/assembly, Swagger và API GET runtime. Swagger trên localhost:5102 hiện thiếu roomAmenities so với source; xác nhận parity và FE proxy target trước UAT. | BE/FE + release owner | Runtime contract khớp source; smoke check ở môi trường được duyệt; FE gọi đúng target. Không dừng process không liên quan. |

## Migration và rollback

- Ưu tiên expand → backfill → contract. Migration additive, nullable/default an toàn; không rename/drop hoặc đổi nghĩa Services.Price cùng đợt.
- Review và chạy staging trước; kiểm tra plan, lock, FK/index và dữ liệu trước/sau. Production cần theo backup/restore policy của vận hành.
- Dùng transaction khi PostgreSQL/provider hỗ trợ và thao tác cho phép; xác minh behavior cho từng index/constraint trước khi chọn cách chạy.
- Rollback ứng dụng cũ phải hoạt động với schema mở rộng. Không dùng Down destructive để xóa Description, rates, amenities hoặc invoice snapshots đã sử dụng. Sửa dữ liệu bằng migration forward-only có phạm vi/audit.
- Với backfill, chuẩn bị SELECT preview, số bản ghi dự kiến, giới hạn khóa và hậu kiểm. Không chạy backfill trong Phase 3.1A.
- Tách reconcile Maintenance migrations/history khỏi Property Detail migration để tránh tạo lại/xóa bảng có dữ liệu.

## Acceptance criteria đề xuấ

1. Live DB, EF model, snapshot và migration history đồng bộ; deploy không tạo lại Maintenance tables.
2. Description nullable đi qua CRUD/public GET; không tạo text giả.
3. PropertyAmenities chỉ chứa link được owner xác nhận; PK/FK ngăn duplicate/orphan; RoomAmenities vẫn là tiện ích theo phòng.
4. Room amenity summary dùng đúng rentability policy; coverage tính trên phòng rentable, không gom phòng không rentable.
5. livingCosts chỉ trả rate đang hiệu lực, có UnitCode/BillingBasis, đúng Property/RoomType scope; không fallback mơ hồ sang Services.Price.
6. RoomTypes.Price/starting rent không đổi; không trộn rent với phí dịch vụ.
7. Invoice cũ giữ nguyên; đổi rate không đổi snapshot đã phát hành; invoice mới dùng rate đúng kỳ.
8. Public API không lộ OwnerId/OwnerName, tenant, booking/contract hay tài liệu riêng; CRUD được authorize theo Property scope.
9. API/FE xử lý đúng 200, 404, null, [] và ảnh; runtime Swagger/API khớp source sau deploy.
10. Kiểm tra RLS/policies và anon/authenticated access riêng; quyền DB role trong audit không đại diện quyền API.

## Release gates

- BLOCK: rate scope, unit/basis hoặc invoice snapshot chưa được Billing/owner chốt.
- BLOCK: Maintenance migration pending chưa reconcile với live schema/history.
- BLOCK: thiếu review migration, backfill preview/hậu kiểm hoặc kế hoạch phục hồi.
- BLOCK: public endpoint lộ dữ liệu nội bộ hoặc rate không có đơn vị/nguồn rõ.
- READY FOR UAT: DB/EF/source đồng bộ; API contract và runtime parity xác nhận; FE render đủ trạng thái; acceptance checks đạt ở môi trường được duyệt.

Đây là kế hoạch, không phải phê duyệt triển khai hoặc thay thế release checklist.
