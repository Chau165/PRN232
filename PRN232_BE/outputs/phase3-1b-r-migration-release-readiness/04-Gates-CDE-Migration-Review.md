# Gates C/D/E — Rà soát ba migration Phase 3.1B

SQL review ban đầu nằm trong `outputs/phase3-1b-property-rates-invoice-snapshot/SQL_REVIEW/`. Hash SHA-256 chính xác và thứ tự triển khai ở `SQL_REVIEW/03-Production-Gates-CDE-Manifest.md`.

## Gate C — Property description và tiện ích chung

- Thêm `Properties.Description text NULL`.
- Tạo `PropertyAmenities(PropertyId, AmenityId)` với composite PK, hai FK cascade và index theo `AmenityId`.
- Bật RLS cho bảng mới, không tạo policy và không seed dữ liệu.
- Local replay cho kết quả Description vẫn NULL trên 3 fixture properties, bảng tiện ích rỗng.

## Gate D — Bảng giá theo Property/Service

- Tạo `PropertyServiceRates` theo cặp Property/Service, đơn giá `numeric(18,2)`, `UnitCode`, `BillingBasis`, khoảng hiệu lực nửa mở `[EffectiveFrom, EffectiveTo)` và các CHECK non-negative/month-start/đơn vị-basis.
- FK Property/Service dùng RESTRICT; index tra cứu; exclusion constraint GiST ngăn các khoảng hiệu lực chồng lấn cho cùng Property/Service.
- Dùng `btree_gist` cho toán tử equality trên cột integer trong exclusion constraint. Live chưa cài extension; catalog cho thấy các phiên bản trusted nhưng quyền cài trên project chưa xác minh. Local replay thành công bằng superuser PostgreSQL 17.10, không chứng minh quyền Supabase.
- Bảng bắt đầu rỗng. Không tạo mức giá mẫu, không fallback sang `Services.Price`, không suy diễn applicability/bắt buộc từ đơn giá.

## Gate E — Snapshot trên InvoiceDetails

- Thêm nullable `Quantity numeric(18,4)`, `UnitPrice numeric(18,2)`, `UnitCode`, `BillingBasis`, `ServiceId`, `RateId`; FK tới Service và cặp RateId/ServiceId.
- Không backfill 25 hóa đơn cũ; local postflight xác nhận cả 25 dòng giữ snapshot NULL và fingerprint năm cột cũ không đổi.
- Chưa có Billing Engine, invoice recalculation, snapshot immutability trigger, FE, data seed hoặc policy RLS mới trong phase này.

## Thứ tự / transaction

Chạy C rồi D rồi E, mỗi file chính xác một lần và tuần tự; từng file đã bao transaction. C phải có trước D, D trước E. Không chạy các lệnh đồng thời, không chạy lại sau khi history marker đã ghi, không chạy `dotnet ef database update` trên live trong phase này. D có `CREATE EXTENSION IF NOT EXISTS btree_gist` bên trong transaction; cần xác nhận quyền/operation support trước.

## Rủi ro cần approval

- RLS bật nhưng policy bằng 0; public API access phải có owner/security sign-off.
- Quy tắc `unit_code`/`billing_basis` chỉ gồm tập được migration liệt kê; đối chiếu với quyết định nghiệp vụ đã phê duyệt.
- Exclusion constraint là integrity rule mới; xác nhận chưa có bảng/rate ngoại lệ cần nạp cùng migration. Không có rate nào được seed trong phase này.
- Khi SQL thành công, các history rows C/D/E phải khớp đúng ID/ProductVersion; không chấp nhận sửa nội dung/hashes sau phê duyệt.
