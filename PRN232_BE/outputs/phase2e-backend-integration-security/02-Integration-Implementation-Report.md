# Phase 2E — Báo cáo tích hợp và triển khai

## Phạm vi đã làm

- Đối chiếu bốn public routes với controller → service → repository → `RoomAvailabilityQueries` → DTO; giữ nguyên N-layer boundary và coding standard của repo.
- Bổ sung một PostgreSQL HTTP integration test trong `PRN232.Application.Tests/PublicPropertyListingPostgreSqlTests.cs`. Test chạy bốn GET qua `WebApplicationFactory`, không bearer token; kiểm tra response allow-list, ảnh/thiết bị rỗng và `POST /api/rooms` trả `401`.
- Test host dùng test-only `DbConnectionInterceptor` đặt `SET SESSION CHARACTERISTICS AS TRANSACTION READ ONLY`, sau đó xác nhận `SHOW default_transaction_read_only` là `on` trước khi gửi request. Kết nối lấy từ cấu hình sẵn có chỉ trong process; không ghi/in connection string.
- Lập consumer matrix cho toàn bộ anonymous GET và đọc các trang FE liên quan. Không sửa production BE, FE, schema, migration, seed hay business data.

## Thay đổi source

Chỉ file test hiện có được mở rộng. Không đổi controller, service, repository, DTO, authorization, public response, role, DI, logging hay cấu hình production. Phase 2A–2D và các thay đổi đang có trước task được giữ nguyên.

Không harden legacy `GET /api/properties`, `GET /api/rooms` và route con: FE trong checkout không gọi API, nhưng consumer ngoài checkout/deployed là **UNKNOWN**; đồng thời GET management hiện không có owner scope cho Manager. Chỉ thêm `[Authorize]` sẽ không sửa IDOR/owner filtering và có thể làm hỏng consumer chưa kiểm kê. Không có owner approval cho breaking change.

Ngoài phạm vi GET audit, source cho thấy `POST /api/roles` cũng không có `[Authorize]`. Ghi nhận là security blocker riêng; không đổi route/authorization khi chưa có quyết định về role-management policy và consumer.

## CodeGraph và giới hạn

Đã thử CodeGraph theo prompt; MCP trả `Transport closed`. Tiếp tục bằng `rg` trên toàn bộ `PRN232.Api/Controllers`, đọc trực tiếp `Program.cs`, các controller/DTO/repository liên quan và tìm consumer trong FE. Đây là truy dấu phạm vi endpoint/consumer, không tuyên bố đã audit mọi source file trong BE.

## TDD

Test bổ sung kiểm chứng hành vi đã tồn tại nên không có production implementation cần RED/GREEN. Hai lần đầu của test mới thất bại do harness: thiếu import JSON extension và cấu hình startup read-only không được PostgreSQL server áp dụng (`SHOW` là `off`); test dừng trước khi gọi endpoint. Đây **không** phải RED hợp lệ của business behavior. Sau khi thay bằng test-only connection interceptor và xác nhận session `on`, HTTP test chạy qua cả bốn route thành công.

## Trạng thái

Build và 198 test cuối cùng pass. PostgreSQL provider query tests và HTTP test chạy SELECT-only; không có DDL/DML. Production security vẫn **BLOCKED** bởi legacy anonymous data exposure, owner/role decisions và `POST /api/roles`. Kết luận cuối: **COMPLETE — PHASE 3 FE DEVELOPMENT READY; PRODUCTION BLOCKED**.
