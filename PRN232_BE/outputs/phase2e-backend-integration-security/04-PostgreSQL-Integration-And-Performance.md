# Phase 2E — PostgreSQL integration, read-only và hiệu năng

## PostgreSQL và read-only guard

- Dedicated `PRN232_TEST_POSTGRES_CONNECTION` ban đầu không có trong environment. Dùng `ConnectionStrings__DefaultConnection` đã cấu hình, gán vào biến test **chỉ trong process test**; không in/ghi connection string.
- Repository provider tests mở kết nối không pooling, chạy `SET SESSION CHARACTERISTICS AS TRANSACTION READ ONLY`, rồi xác nhận `SHOW default_transaction_read_only = on`.
- HTTP integration test thay DbContext trong test host bằng Npgsql, cài interceptor đặt session read-only trên mỗi kết nối và xác nhận `SHOW ... = on` trước route request. Startup option `Options=-c default_transaction_read_only=on` trả `off` trên server; test dừng trước khi gọi API, sau đó harness được sửa dùng `SET SESSION`.
- Bốn HTTP GET được gọi qua Npgsql thật; management `POST /api/rooms` bị authorization middleware từ chối `401`. Không có DDL/DML/seed/status mutation. Test không ghi giá trị property/room/ảnh ra output.

## SQL shape và bounded SELECT

| Luồng | Bằng chứng provider | SELECT |
|---|---|---:|
| Property listing repository call | Count, page aggregate/projection, page-batched amenity query. Filter/price/count dịch SQL; `NOT EXISTS`, `COUNT`, `MIN`, `LIMIT`, `OFFSET`. | 3 mỗi call; test hai call ghi nhận 6 |
| Property detail | Một projection bao gồm eligibility, distinct count và min price. | 1 |
| Property rooms | Eligibility check, filtered distinct count, page projection, amenity batch trong `RepeatableRead`. | 4 |
| Unknown detail / unknown room list | Lookup/eligibility query, không trả resource. | 1 mỗi request |
| Room detail | Projected Room/Property/RoomType + Phase 2A predicate; amenity batch; service batch theo exact RoomTypeId, trong `RepeatableRead`. Không query Images/Equipments. | 3 khi rentable; 1 khi blocked/unknown |

HTTP service có thể chạy thêm một `AmenityIdsExistAsync` SELECT trước repository khi caller gửi `amenityIds`; đây là validation bounded, ngoài số statement của repository test. Amenities/services không N+1. Request dùng cùng `asOfUtc`; listing và room list dùng transaction `RepeatableRead`; property detail là một SQL statement.

## Dữ liệu ảnh và kế hoạch query

Read-only aggregate hiện tại (2026-10-19 UTC), chỉ log count: 12 RoomImages; 12 HTTPS/scheme tuyệt đối; 1 distinct host; 0 Supabase host/public/signed path, local/private host, query string hoặc signed-like query. Không in host/path. Kết quả này **không chứng minh trusted host hay anonymous ACL**; public DTO tiếp tục trả null/[] và không query ảnh.

Không chạy `EXPLAIN` hay `EXPLAIN ANALYZE`; không đo p95/load. Chưa refresh index catalog trong lượt này và không đề xuất migration dựa trên snapshot cũ. Bước tiếp theo là `EXPLAIN` read-only trên staging/non-production, đánh giá indexes theo plan thực tế; không chạy analyze tải nặng trên Supabase live.

## Giới hạn

Các test chứng minh Npgsql execution/query shape trên live PostgreSQL tại thời điểm chạy, không chứng minh production throughput, toàn bộ tổ hợp dữ liệu hoặc mọi runtime config. Host/provider exception path chưa được inject/test; không kết luận stack trace/error body production. Cache middleware/response caching không thấy trong source; public read vẫn có thể stale giữa hai request do đây là snapshot, không phải reservation.
