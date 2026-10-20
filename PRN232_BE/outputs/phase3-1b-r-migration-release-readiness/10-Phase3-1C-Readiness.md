# Phase 3.1C — Điều kiện bắt đầu

Phase 3.1B-R chỉ chuẩn bị database/migration; chưa triển khai endpoint, seed data, Billing Engine hay frontend. Phase 3.1C chỉ nên bắt đầu sau khi live release được owner/DBA phê duyệt và schema postflight xác nhận.

## Đầu vào phải đạt

- [ ] Đóng hết blocker/approval trong `01-Executive-Decision-And-Status.md` và `05-Production-Backup-PITR-Restore-And-Approval-Checklist.md`.
- [ ] Gate B/C/D/E áp dụng theo thứ tự, có backup/restore evidence và postflight thành công.
- [ ] Chốt nguồn dữ liệu và owner cung cấp Description, tiện ích property, mapping service/rate và ngày hiệu lực; không suy diễn từ sample data.
- [ ] Chốt security contract: public/owner/admin visibility, owner scoping, role dùng với Supabase, RLS policy và bảo vệ endpoints.
- [ ] Kiểm tra live API/runtime so với source; health cũ hoặc Swagger không chứng minh database/API release hiện tại.

## Hợp đồng Property Detail kế tiếp

- `Description` lấy từ `Properties.Description`; NULL/blank hiển thị trạng thái chưa có nội dung.
- `commonAmenities` đến từ `PropertyAmenities → Amenities`; `roomAmenities` vẫn lấy theo phòng. Không trộn hai phạm vi.
- `livingCosts` chỉ trả rates Property/Service hợp lệ tại mốc thời gian nghiệp vụ đã thống nhất, với ServiceId, tên, Price, UnitCode, BillingBasis, EffectiveFrom. Chưa có rate thì trả empty state; tuyệt đối không fallback `Services.Price`.
- Không đưa owner/customer/contract data vào public DTO. Không gọi khoản phí là bắt buộc nếu chưa có applicability decision.

## Ranh giới

Giữ `RoomTypes.Price` làm giá thuê public và `Contracts.MonthlyRent` làm giá hợp đồng. Quy trình nhập data/rates, endpoint changes, DTO mapping, policies, FE, và Billing Engine đều là workstream riêng cần scope/approval cụ thể.
