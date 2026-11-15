# Chọn và xác nhận vị trí khu trọ G4–G6

**Tài liệu lịch sử, không còn là luồng hiện hành.** Cơ chế token/API xác nhận và migration metadata dưới đây đã được gỡ theo A1–A2. Xem [API lưu tọa độ ghim hiện tại](PROPERTY-COORDINATES-A1-A2.md). Không chạy lệnh migration cũ bên dưới để cài luồng này.

## Luồng Admin

Trong popup tạo/sửa khu trọ: nhập địa chỉ, chọn tỉnh/phường → Tìm thử → chọn một gợi ý hoặc bấm/kéo ghim thủ công → Xác nhận vị trí → Tạo/Lưu khu trọ. Khi nguồn tìm kiếm không phản hồi, vẫn có thể đặt ghim thủ công. Các gợi ý được ghi rõ là cấp đường phố hoặc chưa xác minh phường; không tự chọn kết quả đầu tiên.

Đổi địa chỉ, tỉnh/phường hoặc ghim làm mất xác nhận cũ. Phản hồi tìm kiếm/xác nhận của địa chỉ cũ được hủy và bỏ qua. Xác nhận hết hạn sau 15 phút, FE yêu cầu xác nhận lại; BE cũng kiểm tra độc lập. Lưu mô tả/tên/ảnh khi địa chỉ và vị trí đã lưu không đổi không cần xác nhận lại.

Ảnh đại diện được xem trước và mở lớn; lỗi chung nằm dưới hàng nút lưu. Lỗi từng trường của nhánh mới được giữ nguyên.

## API và chống giả mạo

`POST /api/properties/location-preview` (Admin) bổ sung `candidates[].selectionToken`, do BE ký cho tọa độ/nguồn kết quả, địa chỉ, mã tỉnh/phường và Admin gọi API. Token này chỉ dùng chọn gợi ý, không dùng trực tiếp để lưu khu trọ.

`POST /api/properties/location-confirmation` (Admin):

```json
{
  "addressText": "121 Lê Văn Việt",
  "provinceCode": "79",
  "wardCode": "<mã phường hiện hành>",
  "latitude": 10.85,
  "longitude": 106.77,
  "selectionToken": null
}
```

- Gợi ý được chọn nguyên tọa độ: gửi selectionToken; BE kiểm tra chữ ký, mục đích, Admin, địa chỉ/mã hành chính, thời hạn và tọa độ. Sai số biểu diễn số từ JS tối đa 0,000001 độ được chấp nhận; tọa độ lưu lấy từ gợi ý đã ký và làm tròn 6 số thập phân.
- Đặt/kéo ghim thủ công: không gửi selectionToken. BE kiểm tra tọa độ trong khung Việt Nam được hỗ trợ (vĩ độ 6,5–24, kinh độ 102–117,5), danh mục tỉnh/phường và quan hệ phường thuộc tỉnh. Nguồn ghi nhận là AdminPinned.
- Response: confirmationToken, expiresAtUtc, source. Gợi ý có nguồn AdminSelected:Nominatim/Photon. Không nhận cờ confirmed hay nguồn dữ liệu do FE tự khai.
- Đây **không phải xác minh pháp lý hay kiểm tra ranh giới polygon phường**. Dữ liệu hiện không có ranh giới đó; Admin phải đối chiếu ghim với địa chỉ thực tế. Khung tọa độ chỉ chặn vị trí bất hợp lý ở xa, không khẳng định mọi điểm trong khung đều thuộc Việt Nam.

Gửi `locationConfirmationToken` trong multipart của POST/PUT `/api/properties`. BE kiểm tra lại token gắn đúng Admin, địa chỉ và mã tỉnh/phường; token sai/hết hạn trả 400 và không fallback âm thầm sang geocoding. Có token hợp lệ thì dùng tọa độ đã xác nhận, không gọi lại Nominatim/Photon. Token không cấp quyền lưu cho Manager/Customer.

Để tương thích API cũ, caller không gửi token vẫn đi qua kiểm tra geocoding tự động cũ; điều này không cho phép tự khai tọa độ. FE mới bắt buộc xác nhận khi tạo hoặc đổi địa chỉ/ghim. Token có thể dùng lại bởi cùng Admin cho cùng địa chỉ/tọa độ trong hạn; đây không phải khóa chống trùng tạo khu trọ.

Token dùng ASP.NET Core Data Protection cùng key ring của BE; không thêm secret mới vào FE. Backend phải giữ key ring khi deploy như cấu hình hiện có.

## Dữ liệu và migration

Properties thêm ba cột nullable: LocationSource, LocationConfirmedByUserId, LocationConfirmedAtUtc. Dữ liệu cũ giữ null; cập nhật tên/mô tả/ảnh không đổi địa chỉ giữ nguyên thông tin xác nhận. Luồng geocoding tự động mới ghi AutomaticGeocoding.

Migration: AddPropertyLocationConfirmation. Đã tạo và kiểm tra model/script; chưa chạy lên Supabase hoặc DB nghiệp vụ. Cần áp dụng trước khi chạy BE mới, tại thư mục gốc repository với đúng cấu hình DB:

```powershell
dotnet ef database update --project PRN232_BE/PRN232.Infrastructure --startup-project PRN232_BE/PRN232.Api
```

## Kiểm thử

BE có test token bị sửa, sai Admin/địa chỉ/mã phường/mục đích, hết hạn, sửa tọa độ gợi ý và tọa độ ngoài phạm vi. Test API xác nhận → tạo/cập nhật khu trọ xác nhận tọa độ và metadata được lưu. Có test selection token đi từ preview đến confirmation và migration khớp model.

FE có test chọn gợi ý, chuyển sang ghim thủ công làm mất proof, nguồn lỗi vẫn đặt ghim, bỏ phản hồi địa chỉ cũ, chặn lưu trước xác nhận và đổi địa chỉ sau xác nhận. Các test dùng mock provider/map, không gọi dịch vụ bản đồ thật hay DB Supabase.

Kiểm tra cuối: 64 test BE về property/geocoding/xác nhận/kiến trúc và 30 test FE liên quan đều pass; TypeScript và production build pass. Hai kiểm tra UI cũ được sửa để đợi đúng nội dung bất đồng bộ và tìm phần tử trong đúng form/drawer. Chưa chạy migration vào DB nghiệp vụ hoặc kiểm thử trên Railway.
