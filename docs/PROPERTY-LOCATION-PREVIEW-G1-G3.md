# Xem trước vị trí khu trọ G1–G3

API mới: `POST /api/properties/location-preview`, chỉ Admin. Không ghi DB, không upload ảnh, không tạo hay sửa khu trọ. Chưa trả chứng nhận vị trí dùng để bỏ qua kiểm tra khi lưu; phần xác nhận/lưu thuộc G5–G6.

```json
{
  "addressText": "121, đường Lê Văn Việt",
  "provinceCode": "79",
  "wardCode": "<mã phường trong danh mục hiện hành>"
}
```

Tỉnh/phường được tra bằng danh mục của BE, kiểm tra quan hệ cha con; không nhận tên địa giới tự khai từ FE. Địa chỉ tối đa 250 ký tự.

Response thành công HTTP 200 có `status`, `originalAddress`, `requiresConfirmation`, `candidates`, `providers`.

| status | Ý nghĩa |
|---|---|
| Matched | Có kết quả khớp bằng chứng số nhà, tên đường, phường và tỉnh ở cấp nhà/tòa nhà; vẫn cần Admin xác nhận |
| Suggestions | Chỉ có vị trí gợi ý: cấp đường phố, chưa xác minh phường, số nhà khác/thiếu, hoặc các kết quả khớp nằm cách nhau trên 100 m |
| NoResults | Có dịch vụ phản hồi nhưng không có kết quả phù hợp để đưa ra; cần xem cả providers để biết tìm kiếm có bị thiếu do lỗi nguồn khác không |
| Unavailable | Không nguồn nào trả được phản hồi tìm kiếm thành công; xem providers để phân biệt lỗi/timeout/giới hạn lượt/budget |

Mỗi candidate chứa `location` (địa chỉ và tọa độ thật của nguồn), `matchLevel` (`Match` hoặc `Suggestion`), `reasons` (ví dụ `ward_unverified`, `approximate_location`, `house_number_mismatch`, `spatially_ambiguous`). Không tự sinh số nhà gần. Số nhà khác chỉ được đưa ra khi các bằng chứng địa lý còn lại phù hợp; không cam kết đó là căn gần nhất theo khoảng cách thực tế.

`providers` liệt kê các trạng thái đã gặp của mỗi nguồn: Success, Empty, Timeout, Unavailable, RateLimited, BudgetExceeded. Có thể có nhiều trạng thái trên cùng nguồn do thử nhiều biến thể. Khi một nguồn lỗi mà nguồn khác có gợi ý, `status` là Suggestions, không che gợi ý bằng lỗi chung 502.

## Tìm kiếm

Tìm nguyên bản, chuẩn hóa khoảng trắng/bỏ dấu phẩy giữa số nhà và tên đường, thử bỏ tiền tố “đường” rồi mở rộng tìm cùng đường/khu vực không kèm số nhà. Giữ nguyên tên “Đường số 8”, “Đường 8”. Dữ liệu Admin nhập không bị thay đổi. Truy vấn trùng được loại bỏ; Photon dùng dữ liệu có cấu trúc nên không lặp lại biến thể chỉ khác dấu phẩy.

Tối đa 4 biến thể Nominatim và 3 biến thể Photon; 4 giây mỗi lần, tổng ngân sách 25 giây. Nguồn lỗi hoặc bị giới hạn lượt không được thử lại liên tục trong cùng yêu cầu. Dùng cache và giới hạn tốc độ Nominatim đã có của gateway. Hủy request từ client được truyền tiếp, không chuyển thành kết quả NoResults. Không gọi thêm nhà cung cấp mới hay yêu cầu key mới.

Kết quả khác tỉnh/phường/đường đã xác minh, sai quốc gia/tọa độ hoặc độ chính xác quá thấp không được trả thành gợi ý. Thiếu bằng chứng phường được phân biệt với khẳng định thuộc phường khác.

## Tương thích luồng lưu

Luồng tạo/sửa vẫn kiểm tra vị trí tự động như trước, không chấp nhận ghim tự khai. Chỉ thay thứ tự phân loại lỗi: nếu đã có kết quả nhưng chưa đủ tin cậy/không rõ vị trí, trả lỗi validation 400, thay vì để lỗi nhà cung cấp còn lại che thành 502. Không cần migration.

G4 chưa triển khai: chưa có nút Tìm thử, danh sách gợi ý hoặc bản đồ mới trên FE. Có thể thử API qua Swagger với token Admin.

## Kiểm thử

PropertyLocationPreviewTests kiểm tra quyền API, biến thể truy vấn, tên đường có số, Nominatim timeout + Photon gợi ý, địa giới sai, số nhà lân cận thật, kết quả xa nhau, hủy request và thiếu dịch vụ. PropertyGeocodingSelectionTests bổ sung regression cho trường hợp lỗi nguồn không che kết quả chưa xác minh của nguồn dự phòng. Toàn bộ dùng fake provider, không gửi yêu cầu ra dịch vụ bản đồ thật.
