# API lưu tọa độ ghim A1–A2

## Thay đổi

POST /api/properties và PUT /api/properties/{id} tiếp tục chỉ dành cho Admin, dùng multipart/form-data và giữ nguyên xử lý ảnh, tên/mô tả và kiểm tra mã tỉnh/phường. Bổ sung latitude, longitude nullable trong DTO/form; không nhận tọa độ qua token hay cờ confirmed. OwnerId vẫn lấy từ người đăng nhập.

```text
propertyName: Trọ Việt
addressText: 121 Lê Văn Việt
provinceCode: <mã tỉnh từ danh mục>
wardCode: <mã phường thuộc tỉnh>
latitude: 10.851234
longitude: 106.771234
description: ...
primaryImage: <file, không bắt buộc>
```

Tọa độ dùng dấu chấm thập phân, độc lập thiết lập ngôn ngữ của máy chủ. NaN, Infinity, chuỗi không phải số, cặp tọa độ thiếu và tọa độ ngoài phạm vi đều trả 400. Phạm vi hỗ trợ: vĩ độ 6,5–24; kinh độ 102–117,5. Lưu làm tròn 6 chữ số thập phân, tương ứng cột numeric(9,6) hiện có. Khung này không chứng minh điểm thuộc Việt Nam hay đúng polygon phường; Admin phải đối chiếu thực tế.

| Thao tác | Quy tắc |
|---|---|
| Tạo mới | Bắt buộc đủ latitude và longitude |
| Sửa tên/mô tả/ảnh, địa chỉ và mã không đổi | Có thể không gửi tọa độ; giữ nguyên tọa độ cũ |
| Đổi địa chỉ hoặc mã tỉnh/phường | Bắt buộc gửi tọa độ; kiểm tra danh mục và quan hệ phường thuộc tỉnh |
| Chỉ đổi ghim | Gửi đủ hai tọa độ; không ghi đè địa chỉ |
| Chỉ gửi một tọa độ | Trả 400, không cập nhật dữ liệu |

Không còn gọi Nominatim/Photon trong PropertyService.CreateAsync/UpdateAsync. Dịch vụ tìm địa chỉ không khả dụng không chặn lưu ghim hợp lệ. API lưu không kiểm tra lại tên đường/số nhà bằng dữ liệu bản đồ, không tự thay vị trí Admin chọn. Client cũ không gửi tọa độ khi tạo/đổi địa chỉ sẽ nhận 400, không fallback âm thầm.

## Gỡ luồng xác nhận cũ

Đã gỡ endpoint location-confirmation, token selection/confirmation, signer/validator token, DTO và các trường metadata vị trí. Không thêm secret. Đã đọc lịch sử migration của DB cấu hình: AddPropertyLocationConfirmation ở trạng thái Pending. Vì vậy chỉ gỡ migration chưa áp dụng và ba bổ sung model/snapshot tương ứng; không rollback hay cập nhật DB. Không cần migration mới cho A1–A2 vì Properties đã có Latitude/Longitude.

## Giao diện trong đợt này

Gỡ bước Xác nhận vị trí và thời hạn token. Form gửi tọa độ hiện tại trực tiếp khi chọn gợi ý hoặc chỉnh ghim; giữ tính năng ảnh phóng to và lỗi trong popup. Địa chỉ mới làm mất tọa độ đã chọn, cần chọn lại trước khi lưu. Đây là điều chỉnh tương thích tối thiểu để UI không gọi API đã gỡ.

Tìm thử hiện vẫn dùng API preview BE đã có. Chuyển tìm kiếm/gợi ý sang gọi dịch vụ trực tiếp ở FE là A3, chưa thực hiện. API preview vẫn được giữ để không cắt chức năng tìm kiếm hiện hữu, nhưng không còn token và không tham gia kiểm tra khi lưu.

## Kiểm thử

PropertyCoordinateApiTests kiểm tra tạo/sửa ghim, giữ tọa độ khi bỏ cặp trong cập nhật không đổi địa chỉ, bắt buộc tọa độ khi đổi địa chỉ/mã, phân quyền, số không hợp lệ và model khớp migration. Các test ảnh/địa chỉ cũ được cập nhật dùng tọa độ trực tiếp. Test lựa chọn bằng chứng geocoder chỉ còn phục vụ preview.

FE kiểm tra chọn gợi ý → kéo ghim → gửi tọa độ cuối cùng, không còn bước xác nhận; bỏ kết quả tìm kiếm của địa chỉ cũ; giữ popup ảnh và lỗi. Không gọi dịch vụ bản đồ thật hay ghi dữ liệu lên Supabase trong kiểm thử.

Kết quả kiểm tra: nhóm BE mở rộng 111 test pass, FE liên quan 20 test pass, typecheck và build pass. Nhóm API tọa độ/preview được chạy lại sau dọn interface. Lịch sử migration của DB đã được kiểm tra chỉ đọc; không thay đổi dữ liệu DB.
