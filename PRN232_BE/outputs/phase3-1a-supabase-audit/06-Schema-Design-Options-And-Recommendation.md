# Phase 3.1A — Schema Design Options and Recommendation

Thiết kế dưới đây chỉ để owner/Billing review. Không có DDL nào được chạy.

## Giới thiệu

Ưu tiên thêm Properties.Description kiểu text nullable. Đây là thuộc tính mô tả của một Property, không cần bảng riêng. Cần cập nhật Domain entity, create/update DTO/service/repository và public detail projection theo N-layer trong CODING_STANDARDS.md. Chỉ nhập nội dung đã được chủ khu trọ xác nhận; không seed copy marketing suy đoán.

## Tiện ích chung và tiện ích phòng

Giữ Amenities làm catalog chung và RoomAmenities cho tiện ích phòng. Nếu cần danh sách tiện ích property-level, thêm PropertyAmenities với PK(PropertyId, AmenityId), FK đến Properties/Amenities, index theo AmenityId và CRUD được scope theo owner/Manager. Không backfill từ RoomAmenities.

Public API nên giữ hai danh sách tách biệt:

- commonAmenities: tiện ích property-level do người quản lý xác nhận.
- roomAmenities: tiện ích gắn với các phòng hiện rentable, kèm rentableRoomCount và tổng availableRoomCount để hiển thị coverage chính xác.

Không gọi một amenity là “chung” chỉ vì một phòng có link.

## Chi phí — so sánh ba lựa chọn

| Option | Nguồn rate | Migration cần không? | Tương thích / rủi ro |
|---|---|---|---|
| A — Services.Price toàn cục | Một Service có một giá dùng cho mọi RoomType/Property; thêm UnitCode và BillingBasis vào Services để diễn giải. | Có nếu thêm metadata đơn vị. Không đổi schema thì chỉ trả Price thô, chưa đủ an toàn để hiển thị rate. | Ít bảng nhất; không phù hợp khi giá thay theo Property; update giá hiện tại có thể gây hiểu lầm lịch sử. |
| B — Giá theo RoomType | Mở rộng RoomTypeServices bằng Price/UnitCode/BillingBasis hoặc thêm bảng rate theo RoomType+Service. | Có. | Dịch vụ áp dụng theo loại phòng; RoomTypeId có thể dùng bởi nhiều Property, khiến giá chia sẻ. Chỉ chọn khi Billing xác nhận giá phụ thuộc RoomType. |
| C — Rate theo Property+Service | PropertyServiceRates có PropertyId, ServiceId, Price, UnitCode, BillingBasis, EffectiveFrom, EffectiveTo; rate đang hiệu lực duy nhất cho một Property+Service tại một thời điểm. | Có; thêm API quản lý rate và migration. | Phù hợp nhất nếu điện/nước/phí do từng khu trọ thiết lập. Cần ngăn khoảng hiệu lực chồng lấn và giữ snapshot trên Invoice. |

### Recommendation

Chọn C nếu owner xác nhận rate có thể khác theo khu trọ. Nếu Billing xác nhận đơn giá hiện tại dùng chung và có quy tắc thống nhất, Option A có thể đơn giản hơn nhưng vẫn cần UnitCode/BillingBasis. Không chọn Option B chỉ vì RoomTypeServices đã tồn tại.

### Services.Price khi chuyển sang Option C

1. Giữ cột và REST field cũ trong giai đoạn tương thích; xác định nó là legacy/catalog default.
2. Không dùng làm rate niêm yết hoặc đầu vào tính Invoice mới sau cutover.
3. Không fallback âm thầm sang Services.Price khi PropertyServiceRates thiếu; trả livingCosts rỗng/trạng thái chưa cấu hình.
4. Không dual-write Price vào hai nguồn. Backfill chỉ từ rate được owner/Billing xác minh.
5. Sau khi kiểm kê client cũ, mới quyết định deprecate hoặc bỏ Price trong một phase riêng.

RoomTypeServices vẫn có thể quyết định dịch vụ áp dụng cho RoomType nếu Billing duyệt quy tắc đó. Nếu điện/nước là phí property-wide độc lập RoomType, cần biểu diễn applicability theo Property. Parking cần catalog/rate được xác nhận; hiện không có Parking trong Services.

UnitCode/BillingBasis phải biểu diễn tối thiểu: VND/kWh, VND/m3, VND/người/tháng, VND/phòng/tháng và phí xe theo chính sách đã chốt. Không encode unit vào Name. Rate hiệu lực cần EffectiveFrom/To và cơ chế DB/API ngăn overlap. Khi tạo invoice, snapshot RateId/ServiceId, UnitCode, Quantity, UnitPrice và Amount; giữ Description/Amount hiện tại để client/report cũ còn đọc được. Không sửa invoice đã phát hành khi đổi rate.

Không thay RoomTypes.Price; đây vẫn là rent cơ bản theo RoomTypeId. Không dùng duplicate TypeName làm key giá.

## API/FE đề xuấ

Mở rộng GET /api/public/properties/{id} mà không xóa field hiện tại:

- description nullable.
- commonAmenities array; [] nếu chưa cấu hình.
- roomAmenities array theo source hiện tại; [] nếu chưa có link cho phòng rentable.
- livingCosts array chỉ từ nguồn rate chính thức đã được xác minh; [] nghĩa là chưa có cấu hình hiển thị.

Mỗi livingCosts item tối thiểu có serviceId, name, price, unitCode, billingBasis, effectiveFrom. Không trả OwnerId/OwnerName, customer, booking, contract hoặc payment details. Scope mọi query bằng route PropertyId.

Giữ 200 khi Property public và có ít nhất một phòng rentable; null description và [] không làm lỗi trang. Giữ generic 404 cho Property lạ/không còn rentable theo contract hiện có cho đến khi owner duyệt khác.

PropertyDetailPage mapping: Giới thiệu từ description; Tiện ích chung từ commonAmenities; Tiện ích trong phòng từ roomAmenities; Chi phí sinh hoạt từ livingCosts. Không hardcode giá hoặc tự thêm đơn vị khi unitCode chưa có.

## Blocker schema/migration trước Phase 3.1B

Source có migration ReconcileMaintenanceReportsAndLogNullability chưa được ghi vào history live, dù hai bảng migration muốn tạo đã tồn tại. Một số index/default lệch. Trước khi generate/apply migration mới, cần kế hoạch reconciliation riêng; không chạy database update mù quáng.
