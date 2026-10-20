# PostgreSQL provider test matrix và compatibility

## Môi trường cô lập

- PostgreSQL 17.10 trên loopback 127.0.0.1:55432; database fixture và server được tạo riêng cho lượt này.
- Full migrated fixture được áp dụng từ các EF migrations hiện có; synthetic data riêng.
- Enriched và legacy integration tests tự tạo database ngẫu nhiên rồi drop trong finally.
- API/SQL read paths trong provider tests đặt session READ ONLY. Migration/test setup chỉ ghi vào PostgreSQL local.
- Không kết nối Supabase live; không dùng live URL trong test; không in secret hoặc dữ liệu người thuê.

## Test matrix

| Trường hợp | Kết quả |
|---|---|
| Full migrated PostgreSQL, flag true | API trả description, common amenities, room amenities, rates theo Property; 5 SELECT bounded |
| RoomTypes.Price và tập rentable | StartingPrice giữ giá thấp nhất trong các phòng rentable, không đổi Phase 2A |
| Booking/Contract/status | Phòng bị chặn không vào count/amenities/starting price |
| Rates | Property scope, hiện tại/tương lai/hết hạn, [from,to), Asia/Ho_Chi_Minh boundary; Services.Price không fallback |
| Empty/404 | Description null, arrays []; unknown/no rentable giữ generic 404 |
| Legacy schema trước enrichment, flag false | Listing, property detail, property rooms, public room detail và management GET Property/Room thành công; SQL không truy cập cột/bảng mới |
| Legacy full Property entity | GetEntityByIdAsync trả SQLSTATE 42703 thiếu Description; management write path bị chặn khi deploy code mới lên schema cũ |
| Test riêng API/PG cho public browsing | 50 passed, 0 failed, 0 skipped |
| Full solution regression | 203 passed, 0 failed, 0 skipped |

## Lệnh xác minh

- dotnet build PRN232_BE.sln --no-restore --verbosity minimal — 0 warning, 0 error.
- dotnet ef migrations has-pending-model-changes --project PRN232.Infrastructure --startup-project PRN232.Api — không có pending model changes.
- dotnet test PRN232_BE.sln --no-build --no-restore --verbosity minimal — 203/203.
- Các biến PRN232_TEST_POSTGRES_CONNECTION, PRN232_MIGRATION_TEST_POSTGRES_CONNECTION và ConnectionStrings__DefaultConnection đều trỏ 127.0.0.1:55432. Không sử dụng connection string live.

## Giới hạn

Legacy matrix mô phỏng schema theo migration target 20261019034905_AddPropertyPrimaryImageUrl, tương ứng schema pre-enrichment đã biết từ báo cáo Phase 3.1B-R. Không phải kết nối hoặc probe live trong task này. Kết quả không xác nhận migration đã triển khai, data thật đã có, quyền RLS/runtime role, hay FE browser UAT.