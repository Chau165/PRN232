# Phase 3.1A — Owner and Team Decisions

Trạng thái: các quyết định dưới đây chưa được xác nhận. Đây là đầu vào để duyệt Phase 3.1B, không phải thay đổi đã triển khai. Xem báo cáo 02–06 để biết bằng chứng live/source.

| Ưu tiên | Câu hỏi cần chốt | Lựa chọn | Khuyến nghị | Nếu chưa chốt |
|---|---|---|---|---|
| BLOCKER | Rate điện, nước và phí thuộc phạm vi nào? | A. Toàn hệ thống qua Services. B. Theo RoomType. C. Theo Property + Service, có hiệu lực theo thời gian. | C nếu chủ trọ tự đặt giá; chọn A hoặc B chỉ khi Billing xác nhận phạm vi đó. | Không thể thiết kế nguồn giá chính thức hoặc trả livingCosts chính xác. |
| BLOCKER | RoomTypeServices thể hiện dịch vụ áp dụng hay cả giá? | Chỉ applicability; hoặc applicability cùng rate. | Giữ RoomTypeServices làm quan hệ áp dụng; tách rate theo scope Billing đã duyệt. | Link hiện có không chứng minh giá theo Property. |
| BLOCKER | Đơn vị và cơ sở tính của từng dịch vụ? | Đồng/kWh, đồng/m³, đồng/người/tháng, đồng/phòng/tháng, phí xe theo xe/tháng hoặc cách khác. | Billing/owner xác nhận UnitCode, BillingBasis, kỳ tính, làm tròn và dịch vụ bắt buộc/tùy chọn. | Không được hiển thị đơn vị hoặc tính hóa đơn chỉ dựa trên Service.Name. |
| BLOCKER | Hóa đơn lưu snapshot nào? | Mở rộng InvoiceDetails; hoặc mô hình invoice line có snapshot. | Billing chọn mô hình ghi nhận Service/rate, mô tả, quantity, unit, unit price, amount và kỳ tính tại thời điểm phát hành. | Không thể triển khai Billing/rates an toàn hoặc bảo toàn hóa đơn cũ. |
| BLOCKER trước EF release | Xử lý migration Maintenance đang pending trong khi bảng đã có trên live? | Reconcile schema/history bằng thay đổi tiến về phía trước; hoặc remediation riêng được review. | DB/BE xác nhận kế hoạch trước mọi lệnh EF database update. Migration pending có CreateTable cho bảng Maintenance đã tồn tại live. | Có thể lỗi migration hoặc ảnh hưởng dữ liệu Maintenance; xem báo cáo 07. |
| CẦN CHỐT | Có hiển thị tiện ích chung và ai xác nhận? | Nhập PropertyAmenities đã xác nhận; hoặc hoãn section. | Nếu cần, dùng Amenities catalog và bảng PropertyAmenities riêng; owner/manager xác nhận từng link. | Không có dữ liệu chứng minh tiện ích phòng nào là tiện ích chung. |
| CẦN CHỐT | Service nào xuất hiện trong Chi phí sinh hoạt? | Tất cả; hoặc chỉ phí công khai có đơn vị và rate hợp lệ. | Chỉ trả rate public đang hiệu lực; tách tiền thuê khỏi phí dịch vụ. | Services hiện có thiếu metadata đơn vị. |
| CẦN CHỐT | Vai trò Services.Price nếu chọn rate theo Property? | Catalog default/legacy; ngừng ghi; hoặc deprecate sau chuyển đổi. | Chốt một hướng; không dual-write và không fallback âm thầm từ API public mới. Gỡ bỏ riêng sau khi xác định mọi reader/writer. | Có thể có hai nguồn giá mâu thuẫn hoặc cập nhật global làm đổi giá hiển thị. |
| NONBLOCKER cho schema | Nội dung Description và ảnh public nào? | Owner cung cấp nội dung; để null; dùng PrimaryImageUrl hiện có. | Owner duyệt nội dung, Description nullable. Không tự tạo marketing text hoặc công khai storage riêng tư. | Có thể chờ nội dung mà không cần sample data. |
| CẦN CHỐT contract | Property hết phòng có còn trả 404? | Giữ hành vi hiện tại; hoặc trả chi tiết Property không có phòng rentable. | Giữ hành vi hiện tại trong Phase 3.1B để giới hạn scope; thay đổi riêng sau owner approval. | Không ảnh hưởng schema mới nhưng ảnh hưởng endpoint acceptance. |
| PHẢI GIỮ | Có đổi RoomTypes.Price/giá thuê? | Không đổi trong Property Detail. | Giữ starting rent là giá thấp nhất của phòng rentable theo policy hiện tại. | Tránh trộn rent với living costs hoặc đổi Booking/Contract. |

## Khuyến nghị để phê duyệ

1. Chọn Option C nếu rates do từng Property thiết lập; nếu chọn A/B, Billing xác nhận và ghi rõ phạm vi.
2. Giữ RoomTypeServices cho applicability nếu nghiệp vụ hiện tại đúng; lưu giá tại scope đã duyệt.
3. Chốt unit, basis, hiệu lực, kỳ billing, rounding và invoice snapshot trước migration.
4. Nếu cần tương thích, giữ Services.Price như legacy/catalog default trong giai đoạn chuyển tiếp. API không fallback khi thiếu rate đã duyệt.
5. Bảo toàn hóa đơn cũ; không tái tính hóa đơn phát hành bằng rate mới.
6. Không tự gán Description, common amenities hoặc rate cho Bình An, Sunrise, Minh Tâm; chỉ nhập dữ liệu được owner xác nhận.

## Người phê duyệ

- Chủ dự án/owner: nội dung Property, tiện ích chung, dịch vụ công khai, hành vi Property hết phòng.
- Nhóm Billing: scope rate, đơn vị/basis, chu kỳ, rounding, hóa đơn snapshot.
- DB/BE: reconcile migration drift, model/migration/API và release order.
- FE: trạng thái null/empty và nhãn đơn vị.

Phê duyệt các quyết định này không đồng nghĩa với việc đã cho phép chạy migration trong Phase 3.1A.
