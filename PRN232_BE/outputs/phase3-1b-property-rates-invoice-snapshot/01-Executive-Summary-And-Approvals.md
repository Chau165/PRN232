# Phase 3.1B — Tóm tắt và trạng thái phê duyệt

**Trạng thái:** LOCAL MIGRATIONS PREPARED — LIVE APPLY NOT AUTHORIZED  
**Ngày kiểm tra:** 2026-10-19  
**Phạm vi:** Entity/EF model, ba migration cục bộ, PostgreSQL test độc lập và gói SQL review. Không ghi lên Supabase live; không sửa API/FE, không chạy Billing Engine, không tạo giá mẫu, không cập nhật hóa đơn cũ, không commit/push/stage/deploy.

## Quyết định nghiệp vụ đã được duyệt

| Mục | Quyết định | Thể hiện trong schema |
|---|---|---|
| Nguồn giá | Mỗi Property có giá riêng theo Service | PropertyServiceRates(PropertyId, ServiceId, thời hạn) |
| Sáu chế độ áp dụng | Điện/kWh, nước/m3, Internet/phòng/tháng, rác/phòng/tháng, phí/người/tháng, gửi xe/xe/tháng | Năm cặp mã đơn vị/cơ sở; Internet và rác cùng dùng room_month/fixed_room |
| Thời điểm đổi giá | Chỉ từ đầu tháng kế tiếp, theo Asia/Ho_Chi_Minh | EffectiveFrom/EffectiveTo là date đầu tháng; kiểm tra “tháng sau” thuộc lớp dịch vụ tạo giá ở phase sau |
| Snapshot hóa đơn | Lưu số lượng, giá đơn vị, mã đơn vị, cơ sở tính, thành tiền; ServiceId/RateId nếu có | Các cột mới nullable để tương thích hóa đơn lịch sử |

RoomTypes.Price tiếp tục là giá thuê công khai; Contracts.MonthlyRent là giá thuê theo hợp đồng. Services.Price vẫn tồn tại cho tương thích và là dữ liệu catalog cũ, không dùng làm giá fallback mới.

## Đã chuẩn bị

1. 20261019143543_AddPropertyDescriptionAndAmenities — Description nullable và PropertyAmenities.
2. 20261019143615_AddPropertyServiceRates — bảng giá theo Property/Service, ràng buộc đơn vị, thời hạn, không giao nhau.
3. 20261019143633_AddInvoiceDetailBillingSnapshot — cột snapshot nullable và khóa tham chiếu.

Entity, DbSet, Fluent mapping và ModelSnapshot đã đồng bộ; EF báo không có thay đổi model chưa tạo migration. PostgreSQL test loopback kiểm tra migration sạch, fixture giống live, dữ liệu cũ, constraint và concurrency.

## Cổng live

**LIVE APPLY BLOCKED** vì migration Maintenance có trong source nhưng thiếu trong live history trong khi hai bảng Maintenance đã có dữ liệu và sai khác catalog. Không chạy dotnet ef database update, không thực hiện SQL ghi, không sửa __EFMigrationsHistory. Btree_gist chưa cài trên live; catalog cho thấy extension trusted và role có quyền CREATE, nhưng không có phép CREATE thử trên live.

Gate B (Maintenance) và các Gate C–E (ba migration) chưa được phê duyệt thực thi bởi yêu cầu này. SQL trong SQL_REVIEW chỉ để đọc/review; cần phê duyệt riêng sau khi đối chiếu backup/PITR và preflight.

## Các vấn đề còn mở

Mười quyết định Billing vẫn pending: nguồn quantity người/xe; applicability mandatory/opt-in; meter reading thiếu/sai/rollover; lần cấu hình giá đầu; partial month; làm tròn/thuế/điều chỉnh; vòng đời Draft/Issued/Paid và idempotency; hóa đơn lịch sử; catalog Parking/phí người; chuyển đổi consumer khỏi Services.Price. Chi tiết tại 07-Billing-Remaining-Decisions.md.

## Bàn giao

Xem 02–10 cho bằng chứng và thiết kế; 11–12 cho trạng thái Git và diff chỉ chứa Phase 3.1B; SQL riêng tại SQL_REVIEW/.