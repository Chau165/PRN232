# Phase 3.1B — Bàn giao API và dữ liệu cho Phase 3.1C

## Hợp đồng Property Detail cần mở rộng

DTO hiện có PublicPropertyDetailDto chứa PropertyId, tên/địa chỉ, PrimaryImageUrl, AvailableRoomCount, StartingPrice và RoomAmenities. Phase 3.1C cần xem xét thêm:

- Description: từ Properties.Description; null/empty cần hiển thị trạng thái nội dung chưa nhập.
- commonAmenities: PropertyAmenities → Amenities; không suy ra từ tiện ích phòng.
- roomAmenities: RoomAmenities theo phòng (đang có dữ liệu/consumer riêng); giữ nhãn và phạm vi theo phòng, không gộp thành tiện ích property.
- livingCosts: chỉ rate có cấu hình PropertyServiceRates hợp lệ theo thời điểm nghiệp vụ, với ServiceId, tên mô tả, Price, UnitCode, BillingBasis và EffectiveFrom. Không suy ra mandatory/opt-in từ rate.

Rate hợp lệ không đồng nghĩa khoản phí bắt buộc hay số lượng thực tế. Trước khi hiển thị tổng phí, quyết định applicability và public disclosure. Khi không có rate được xác nhận, thể hiện “chưa có giá”/empty state; tuyệt đối không fallback sang Services.Price. Không đưa dữ liệu owner/customer/contract vào public DTO.

## Xung đột nguồn giá hiện tại

Public room detail source hiện truy vấn RoomTypeServices và trả Service.Price trong PublicRoomServiceDto. ServiceCatalogService và RoomTypeRepository cũng đọc Services.Price. PropertyServiceRates là nguồn giá mới cho Property livingCosts và Billing tương lai; giữ Services.Price để tương thích cho consumer chưa chuyển. Phase 3.1C phải định nghĩa transition theo endpoint/consumer và không dual-write.

RoomTypes.Price vẫn là giá thuê public, Contracts.MonthlyRent là giá thuê hợp đồng. Không đổi rentability, bookings hoặc contract behavior trong migration phase.

## Phạm vi giao nhận

Phase này chỉ chuẩn bị database/EF. Không sửa controller/service/DTO/FE. Các file public browsing hiện có dirty/untracked từ công việc trước cần được giữ nguyên; Phase 3.1C nên bắt đầu bằng cách xác nhận trạng thái đó và API/runtime đang chạy có khớp source hay không. Không xem Swagger/runtime cũ là bằng chứng schema thiếu.

## Dữ liệu và cập nhật

PropertyAmenities/PropertyServiceRates bắt đầu rỗng; Description NULL. Nhập nội dung, tiện ích và rates là phase data riêng sau xác nhận ServiceId, đơn giá và thời hạn. Không seed dữ liệu mẫu hoặc tạo Parking/phí người tại đây.

Nguồn source liên quan: PublicPropertyListingDto/PublicPropertyListingRepository, RoomTypeServices, ServiceCatalogService. Giữ quy tắc public property không có phòng thuê được tiếp tục theo API hiện hành cho đến khi owner duyệt thay đổi riêng.