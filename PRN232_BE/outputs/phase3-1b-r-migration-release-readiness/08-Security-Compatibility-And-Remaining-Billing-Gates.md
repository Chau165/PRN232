# Security, API compatibility và Billing gates còn lại

## API/security

- Source rà soát cho thấy `RolesController` có `POST /api/roles` không có `[Authorize]` ở controller/action. Không thay đổi endpoint trong Phase 3.1B-R.
- Các báo cáo Phase 2E trước đó ghi nhận 21 anonymous GET endpoints; `PropertiesController` list/detail không có authorization/owner scope trong các đường public được rà soát. Đây là inherited audit evidence, không phải full route recount trong lần này.
- Live bật RLS nhưng không có policy trên 24 bảng public. Reports/Images có grants cho một số role nhưng access còn phụ thuộc `BYPASSRLS`; role Supabase dùng bởi API/runtime chưa được kiểm chứng trong release này.
- Vì vậy database regression pass không đồng nghĩa API security pass. Trước production, security owner cần duyệt exposure, authorization, tenant/owner scoping, role runtime và policy design riêng.

## Compatibility

- Không chỉnh BE/FE trong Phase 3.1B-R. Regression backend 200/0 trên local migrated fixture.
- `Properties.Description`, `PropertyAmenities`, `PropertyServiceRates` bắt đầu rỗng/NULL; API/FE cần hiển thị empty state cho tới khi data được nhập và phê duyệt.
- Không fallback `PropertyServiceRates` sang `Services.Price`. Phí theo rate không chứng minh mandatory/opt-in hoặc lượng sử dụng.
- `RoomTypes.Price` vẫn là giá thuê public; `Contracts.MonthlyRent` vẫn là giá trong hợp đồng. Migration không đổi cách thuê phòng, booking hoặc hợp đồng.
- Invoice snapshot columns nullable để giữ tương thích với 25 invoice details lịch sử; không sửa các dòng đó và không triển khai Billing Engine ở phase này.

## Billing/API decisions chuyển sang phase kế tiếp

- Quy định cách property detail hiển thị Description, tiện ích chung (`PropertyAmenities`) và tiện ích phòng (`RoomAmenities`) mà không trộn phạm vi.
- Chỉ hiển thị rates còn hiệu lực theo thời điểm nghiệp vụ đã chốt; xác định applicability, opt-in/required và nội dung công khai trước khi tính tổng phí.
- Chốt mapping ServiceId/UnitCode/BillingBasis và nghiệp vụ từ ngày đầu tháng tiếp theo. Không seed dịch vụ hoặc rate giả.
- Lập transition cho consumers đang đọc `Services.Price`; giữ tương thích cho tới khi từng consumer được đổi có kiểm soát, không dual-write mặc định.
- Xác định invoice snapshot immutability, rounding, quantity source, corrections/credit notes và quyền tạo invoice trước khi bắt đầu Billing Engine.

## Giới hạn bằng chứng

Không gọi API live, không kiểm thử FE/browser, không kiểm thử grant/RLS với role `anon`, `authenticated` hoặc runtime API trên Supabase. Build và test suite là bằng chứng nguồn/test local, không phải production readiness.
