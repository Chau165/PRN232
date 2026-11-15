# Hoàn thiện chọn ghim và lưu khu trọ A4–A5

Trong `/management/properties`, Admin nhập địa chỉ và chọn tỉnh/phường, sau đó chọn gợi ý hoặc bấm/kéo ghim trên bản đồ. Lưu sẽ gửi tọa độ cuối cùng bằng multipart. Kéo ghim giữ nguyên địa chỉ đã nhập. Không có bước xác nhận bằng token.

## Hành vi đã hoàn thiện

- Đổi địa chỉ hoặc mã hành chính xóa lựa chọn ghim trong form. Đổi từ A sang B rồi trở lại A cũng không phục hồi tọa độ đã chọn cho A trước đó.
- Tạo mới hoặc đổi địa chỉ phải chọn ghim. Chỉ sửa tên/mô tả/ảnh thì không gửi tọa độ để BE giữ vị trí cũ.
- Mở lại form sửa hiển thị ghim của dữ liệu vừa tải. Tọa độ có thể được BE làm tròn 6 chữ số; FE dùng giá trị từ response khi mở lại.
- Tìm thử lại cùng địa chỉ giữ ghim đang chọn, kể cả dịch vụ lỗi hoặc không có kết quả. Chỉ chọn một gợi ý mới hoặc thao tác bản đồ mới thay ghim.
- Danh sách đánh dấu gợi ý đã chọn; trạng thái phân biệt vị trí đã lưu, vị trí gợi ý và ghim vừa chỉnh.
- Khóa click/kéo marker khi đang lưu, cả ở callback lẫn trạng thái draggable của Leaflet.
- Tọa độ không hữu hạn/ngoài phạm vi hỗ trợ bị chặn ở FE, giữ kiểm tra độc lập ở BE. Khung tọa độ không xác minh ranh giới phường.

## Kiểm thử

PropertyLocationMap.test.tsx kiểm tra chuyển đổi lat/lng từ sự kiện click/dragend, hiển thị marker đã lưu và khóa chỉnh ghim. PropertyLocationPicker.test.tsx kiểm tra gợi ý, kéo ghim, lỗi tìm kiếm và phản hồi cũ.

PropertyLocationFlow.test.tsx nối form thật, picker thật và service multipart với API mô phỏng: tạo bằng ghim cuối cùng → GET mở lại → đổi tên giữ ghim → chỉ đổi ghim → GET mở lại vị trí mới. Có regression cho trường hợp địa chỉ A → B → A. Bản đồ được thay bằng mock trong test này; không phải thử trực tiếp dịch vụ tile hay Railway.

Không đổi API/DB hoặc tạo migration. A3 đã triển khai sau A4–A5: Tìm thử gọi Photon trực tiếp từ FE. Xem PROPERTY-ADDRESS-SEARCH-A3.md và PROPERTY-LOCATION-ACCEPTANCE-A6.md để biết luồng hiện tại và kết quả kiểm tra.

Kiểm tra: 26 test FE liên quan, 18 test API tọa độ, TypeScript và production build đều pass. Chưa thử trực tiếp trên Railway hoặc với tile/geocoding thật.

## Thử thủ công

1. Tạo khu trọ, chọn địa chỉ và đặt ghim; kéo ghim rồi lưu.
2. Mở lại Sửa: kiểm tra ghim ở vị trí vừa lưu, địa chỉ không bị ghi đè.
3. Đổi tên hoặc mô tả rồi lưu; mở lại phải giữ nguyên ghim.
4. Chỉ kéo ghim rồi lưu; mở lại thấy ghim mới và địa chỉ cũ.
5. Đổi địa chỉ, kể cả đổi lại địa chỉ ban đầu: phải chọn ghim lại trước khi lưu.
