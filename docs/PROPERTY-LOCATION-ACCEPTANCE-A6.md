# A6 — Kiểm thử nghiệm thu luồng địa chỉ và ghim khu trọ

## Phạm vi

Luồng hiện tại: Admin nhập địa chỉ + tỉnh/phường → bấm Tìm thử → FE tìm Photon trực tiếp → Admin chọn gợi ý hoặc đặt ghim thủ công → kéo ghim nếu cần → POST/PUT multipart gửi tọa độ cuối → GET và mở lại vị trí đã lưu.

A6 bổ sung kiểm thử tích hợp nối form, picker, bộ tìm Photon và service multipart thật trong FE. Chỉ sự kiện bản đồ và HTTP được mô phỏng. Kiểm thử API BE riêng dùng TestServer và SQLite cô lập. Đây không phải một test duy nhất chạy xuyên trình duyệt, BE thật và Supabase.

## Ma trận kiểm tra tự động

| Trường hợp | Bằng chứng |
| --- | --- |
| Chỉ tìm khi bấm nút, dùng tên tỉnh/phường, không gửi JWT tới Photon | PropertyLocationFlow + PropertyLocationService |
| Bỏ dấu phẩy, thử bỏ “đường”, giữ “Đường số”, giới hạn tìm lại | PropertyLocationService |
| Chỉ hiển thị gợi ý, chưa chọn thì không cho lưu | PropertyLocationFlow |
| Chọn gợi ý → kéo ghim → lưu tọa độ cuối → GET mở lại giá trị làm tròn | PropertyLocationFlow |
| Kéo ghim không thay địa chỉ đã nhập | PropertyLocationFlow |
| Provider trả 429 → đặt ghim thủ công → vẫn lưu được | PropertyLocationFlow |
| API lưu lỗi → giữ ghim và địa chỉ → thử lưu lại | PropertyLocationFlow |
| Đổi địa chỉ A → B → A không phục hồi ghim cũ | PropertyLocationFlow |
| Kết quả tìm cũ, abort, timeout, lỗi mạng/JSON | PropertyLocationPicker + PropertyLocationService |
| Click/drag đúng lat/lng; khóa thao tác khi đang lưu | PropertyLocationMap + PropertyLocationPicker |
| Chỉ đổi tên giữ ghim; chỉ đổi ghim giữ địa chỉ | PropertyLocationFlow + PropertyCoordinateApiTests |
| Tạo mới/đổi địa chỉ thiếu ghim, tọa độ thiếu một phần/NaN/Infinity/ngoài phạm vi | PropertyCoordinateApiTests |
| Phân quyền Admin; mã hành chính hợp lệ; model khớp migration | PropertyCoordinateApiTests |

## Kết quả đã xác nhận

- 35 test FE liên quan vị trí/form: pass (7 file).
- 33 test BE về tọa độ, địa chỉ và tạo khu trọ: pass.
- 2 test Node managementContracts: pass.
- TypeScript và production build FE: pass.
- Hồi quy toàn bộ FE: 457 test pass trong 59 file (bao gồm bộ 35 test trọng tâm). Chạy bằng `pnpm exec vitest run --maxWorkers=1 --reporter=verbose --hookTimeout=10000 --testTimeout=10000`.
- Hai lần gọi bộ FE đầy đủ trước đó không trả kết quả trong thời gian theo dõi và đã được dừng; lần chạy chi tiết cuối hoàn tất thành công.

## Lệnh chạy lại

Trong PRN232_FE:

```powershell
pnpm run test:property-location
pnpm exec tsc --noEmit
pnpm exec vite build
# Hồi quy toàn bộ FE:
pnpm exec vitest run --maxWorkers=1
node --test tests/managementContracts.test.cjs
```

Từ gốc repository:

```powershell
dotnet test PRN232_BE/PRN232.Application.Tests/PRN232.Application.Tests.csproj -c A6Verification --no-restore --filter "FullyQualifiedName~PropertyCoordinateApiTests|FullyQualifiedName~PropertyAddressValidationTests|FullyQualifiedName~PropertyCreationTests"
```

Cấu hình A6Verification tách output khỏi Debug đang chạy trong Visual Studio. Nếu môi trường Windows không cho ghi Event Log, đặt `Logging__EventLog__LogLevel__Default=None` chỉ trong tiến trình test rồi khôi phục sau đó. Không cần chạy VS bằng quyền Admin hoặc thay cấu hình ứng dụng.

## Nghiệm thu thực tế còn cần xác nhận

Môi trường thực hiện không kết nối được `photon.komoot.io:443`. Vì vậy chưa xác nhận Photon/CORS, tile bản đồ hoặc ghi/đọc Supabase trên Railway. Các mục dưới chưa được đánh dấu pass:

1. Trên môi trường thử có tài khoản Admin: mở tạo khu trọ, nhập địa chỉ thực ở Thủ Đức, chọn tỉnh/phường. Network không có request Photon khi gõ.
2. Bấm Tìm thử: request HTTPS trực tiếp tới Photon, không có Authorization. Không gọi endpoint BE location-preview. Nếu lỗi CORS/CSP, kiểm tra endpoint hỗ trợ CORS và connect-src của hosting.
3. Kiểm tra địa chỉ của gợi ý; chọn rồi kéo ghim tới đúng nhà. Địa chỉ nhập không đổi. Lưu một khu trọ thử, GET/mở lại phải thấy tọa độ đã chọn (6 chữ số thập phân).
4. Đổi tên/mô tả, lưu, mở lại: giữ ghim. Chỉ kéo ghim rồi lưu: địa chỉ giữ nguyên, ghim đổi.
5. Đổi số nhà hoặc tỉnh/phường: phải chọn ghim lại. Đổi A → B → A cũng phải chọn lại.
6. Chặn request Photon hoặc giả lập offline ở request đó: đặt ghim thủ công vẫn lưu được khi API BE còn kết nối. Không dùng chế độ offline toàn trang để kết luận API vẫn phải hoạt động.
7. Thử lưu khi API trả lỗi: giữ nguyên dữ liệu/ghim; khôi phục API và thử lại.
8. Manager/Customer không được tạo/sửa khu trọ qua API. Không đánh giá phân quyền chỉ bằng việc ẩn nút trên UI.

Dùng khu trọ thử riêng. Việc tạo dữ liệu nghiệm thu thật cần được người vận hành thực hiện trên môi trường đã chọn; các test tự động không ghi dữ liệu Supabase/Railway.

Không đổi API, schema hoặc migration trong A6.
