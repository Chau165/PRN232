# PRN232 — Public amenity search verification

Ngày kiểm tra: 09/11/2026. Phạm vi: source local trong `C:\PRN232`.

## Kết quả triển khai

Bộ lọc public được chuyển thành một card gọn, gồm ba trường địa chỉ, dropdown tiện ích và hai nút Xóa bộ lọc / Tìm phòng phù hợp. Không khôi phục bốn ô đã được yêu cầu bỏ ở lần sửa trước. Trang danh sách phòng trong một khu trọ cũng dùng lại dropdown mới; các bộ lọc giá, loại phòng và sắp xếp tại trang này được giữ nguyên. Phần bản đồ của lần sửa trước được giữ nguyên trong task này.

### Frontend files của task này

| File dưới `C:\PRN232\PRN232_FE` | Thay đổi |
| --- | --- |
| `src/components/common/AmenityMultiSelect.tsx` | Component controlled dùng chung, searchable dropdown và chips |
| `src/hooks/usePublicAmenities.ts` | Tải catalog qua public API hook hiện có |
| `src/components/home/FilterChips.tsx` | Ba trường địa chỉ và dropdown tiện ích |
| `src/components/home/SearchSection.tsx` | Card mới, submit draft, bỏ ID lỗi thời, xóa bộ lọc |
| `src/pages/HomePage/HomePage.tsx` | Kết nối xóa bộ lọc với URL và kết quả |
| `src/pages/AvailableRoomsPage/AvailableRoomsPage.tsx` | Dùng dropdown chung và giữ các bộ lọc phòng khác |
| `src/services/publicBrowsingService.ts` | Typed catalog request, kiểm tra response và loại ID trùng |
| `src/utils/publicBrowsingQuery.ts` | Chia sẻ giới hạn 20 tiện ích đang có |
| `src/utils/publicQueryState.ts` | Draft chứa ID dạng số, hydrate từ URL và serialize ID lặp lại |
| `tests/amenityMultiSelect.test.tsx` | Kiểm thử component và bàn phím |
| `tests/publicHome.test.tsx` | Draft/applied, URL, Back/Forward, clear, ID lỗi thời |
| `tests/publicPropertyPages.test.tsx` | Regression danh sách phòng với dropdown mới |
| `tests/publicBrowsingApi.test.ts` | Kiểm tra catalog và dữ liệu API không hợp lệ |

### Backend files của task này

| File dưới `C:\PRN232\PRN232_BE` | Thay đổi |
| --- | --- |
| `PRN232.Infrastructure/Repositories/PublicPropertyListingRepository.cs` | Sửa `ApplyRoomFilters` để kiểm tra cả hai cấp tiện ích |
| `PRN232.Application.Tests/PublicPropertyListingApiTests.cs` | Mixed amenities, same-room AND, catalog động và PostgreSQL cô lập |

Các file map và kiểm thử map đã có thay đổi từ task trước; chúng không thuộc phần sửa mới của báo cáo này.

## API và nguồn dữ liệu

- Danh mục: `GET /api/amenities` → `AmenitiesController` → `AmenityService` → `AmenityRepository` → bảng `Amenities`.
- Danh sách khu trọ: `GET /api/public/properties`.
- Danh sách phòng: `GET /api/public/properties/{id}/rooms`.
- Tái sử dụng endpoint, DTO và API client hiện có. Không thêm endpoint trùng, dependency, migration hoặc thông tin kết nối database vào browser.
- Phiên browser kiểm tra đã nhận 16 tiện ích từ BE đang chạy qua Vite proxy. Không dùng danh sách giả trong production frontend.

## State, chips và tìm kiếm

- Draft lưu `amenityIds: number[]`. Component nhận `selectedAmenityIds` và callback `onChange`; tên không dùng làm định danh.
- Chips chỉ render từ catalog API, có nút xóa riêng và không lồng button trong button.
- Tìm tên trong dropdown không phân biệt hoa thường và dấu tiếng Việt, có xử lý `đ`.
- Chọn/bỏ, tìm trong dropdown và xóa chip chỉ cập nhật draft. Nhấn Tìm phòng phù hợp / Áp dụng bộ lọc mới cập nhật URL và gọi search API.
- Serialize theo dạng `amenityIds=1&amenityIds=7`; không gửi tên, không gửi phần tử rỗng, không gửi filter khi danh sách rỗng. Các ID trong ví dụ chỉ minh họa định dạng.
- URL được hydrate lại khi refresh hoặc Back/Forward. ID không còn trong catalog được cảnh báo, không có chip giả, có thao tác bỏ riêng; lần áp dụng tiếp chỉ gửi ID đã được catalog xác nhận.
- Xóa bộ lọc xóa draft, query parameters tương ứng và tải lại kết quả không lọc; không gọi API ghi/xóa Amenities.
- Giới hạn chọn 20 tiện ích ở FE; validation số lượng, ID dương và ID tồn tại ở BE vẫn được giữ.

## Sửa logic backend

Trước đây, mỗi ID chỉ được tìm trong `room.RoomAmenities`. Hiện tại, mỗi ID được thỏa mãn nếu tồn tại trong `RoomAmenities` của phòng **hoặc** `PropertyAmenities` của khu trọ chứa phòng đó.

Vòng lặp tiếp tục thêm một `Where` cho từng ID, vì vậy mọi ID phải cùng được thỏa mãn trên một phòng và khu trọ của phòng đó. Không ghép tiện ích của nhiều phòng khác nhau để tạo một kết quả giả.

Hai endpoint listing dùng chung hàm này. Luồng `RoomRentabilityPolicy`, booking/contract blockers, price/type/location, ReadModel, mapping service, projection số phòng, pagination và TotalCount được giữ. `Any` sử dụng truy vấn tồn tại thay cho join nhân bản hàng hoặc lọc trong memory. Không bổ sung vòng gọi query theo từng phòng.

## Trạng thái và accessibility

- Có loading, error kèm Thử lại, catalog rỗng, không có kết quả tìm tên, cảnh báo ID lỗi thời; không fallback sang mock data.
- Response không phải catalog hợp lệ bị từ chối bằng lỗi API.
- Combobox có label, `aria-expanded`, `aria-controls`, popup dialog và checkbox native.
- Arrow Up/Down chuyển lựa chọn, Enter chọn, Space dùng hành vi checkbox/button native, Escape đóng và trả focus về trigger. Tab và click bên ngoài đóng dropdown khi rời component.
- Enter trong ô tìm tiện ích chọn kết quả đầu, không vô tình submit form cha.
- Chip có accessible name cho nút xóa và focus ring rõ ràng.
- Desktop: địa chỉ cùng hàng; mobile: xếp dọc, chip xuống dòng, trigger không bị bó hẹp. Dropdown có scroll riêng, không làm nhảy layout.

## Kiểm chứng

| Kiểm tra | Kết quả |
| --- | --- |
| FE `npm run typecheck` | Đạt |
| FE `npm test` | 188 Vitest tests / 28 files đạt; thêm 2 Node contract tests đạt |
| FE `npm run build` | Đạt; còn cảnh báo chunk trên 500 kB |
| BE `dotnet restore PRN232_BE.sln` | Đạt với artifacts path riêng |
| BE `dotnet build PRN232_BE.sln --no-restore` | Đạt, 0 warnings / 0 errors |
| Nhóm public listing + amenity service/repository | 68/68 đạt |
| BE full suite | 646 đạt, 9 bỏ qua, 1 thất bại / 656 tổng |
| PostgreSQL relational integration | Đạt; API thật qua test host và Npgsql trên database cục bộ cô lập |
| Browser với API catalog đang chạy | Nhận 16 tiện ích; tìm không dấu và chọn nhiều chips hoạt động |
| Responsive browser | Desktop, 390×844 và 768×1024; không thấy tràn ngang, popup nằm trong viewport |
| Browser console trong phiên kiểm tra | Không ghi nhận error/warn |
| `git diff --check` | Đạt |

Build/test BE dùng `--artifacts-path "$env:TEMP\prn232-amenity-artifacts"` vì tiến trình API hiện có đang khóa DLL ở thư mục build mặc định. Không dừng tiến trình API của người dùng.

PostgreSQL integration được bật bằng biến `PRN232_AMENITY_TEST_POSTGRES_CONNECTION`. Test chỉ chấp nhận host localhost/127.0.0.1, tự tạo database tên `prn232_amenity_test_<guid>`, tạo schema bằng EnsureCreated, seed fixture, gọi API, rồi drop database trong finally. Không tạo migration. Test đã xác minh:

- Tiện ích cấp khu trọ kết hợp tiện ích cấp phòng.
- Cùng ID xuất hiện ở cả hai cấp không nhân bản phòng.
- Phòng bị booking chặn không được tính.
- Tiện ích nằm ở các phòng khác nhau không thỏa AND.
- TotalCount và hai trang kết quả không trùng khu trọ.

PostgreSQL tạm đã dừng sau kiểm tra. Dữ liệu test không được tạo trong Supabase Live.

## Điểm chưa xác minh / giới hạn

1. Bài `SupabaseReadOnlyInspectionTests.Inspect_configured_supabase_schema_and_migration_history_without_writes` thất bại do thiếu các xác nhận backup/schema mà bài kiểm tra yêu cầu (`PRN232_SUPABASE_BACKUP_VERIFIED`, `PRN232_SUPABASE_SCHEMA_CHANGE_APPROVED`). Bài này thực hiện đọc Supabase; không sửa schema/data. Không chỉnh bài test hoặc gán cờ xác nhận giả để làm suite xanh.
2. Chín kiểm thử PostgreSQL/migration khác bị bỏ qua khi thiếu cấu hình riêng. Không suy diễn kết quả của chúng từ test mới.
3. Source BE đã sửa được kiểm chứng bằng test host và PostgreSQL cô lập. Chưa restart/deploy BE đang chạy để áp dụng DLL mới; không tuyên bố runtime đó đã nhận logic mới.
4. Không thực hiện kiểm thử thủ công bằng screen reader hoặc mọi kích thước thiết bị.

Không thay đổi dữ liệu/schema Supabase Live, không tạo migration, không commit/push.
