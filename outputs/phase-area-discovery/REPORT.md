# PRN232 — Phase Khám phá khu vực

**Trạng thái:** Đã triển khai cục bộ; chưa commit, push hoặc deploy. Không có migration hay thao tác ghi lên Supabase.

## 1. Audit source và nguyên nhân ban đầu

- **Backend:** `PublicPropertiesController` đã có public listing và property detail; `PublicPropertyListingRepository` đã dùng `RoomAvailabilityQueries.GetRentableRooms` cho public browsing. Chưa có API tìm property theo khu vực.
- **Frontend:** `MapSection` là placeholder, chưa xin vị trí, gọi API khu vực hoặc render map. Public API client đã là điểm tích hợp cho listing/detail nên được mở rộng tại đó.
- **CodeGraph:** đã thử dùng index BE có sẵn, nhưng truy vấn không truy ra đáng tin cậy call path cho endpoint/query cần sửa. FE không có index `.codegraph`. Vì vậy đã xác minh trực tiếp controller → service → repository, API client → `MapSection` → map component và các call site liên quan.
- **Nguyên nhân:** thiếu cả API hợp đồng khu vực phía BE và luồng vị trí/map/list phía FE; không phải radius filter đang áp sai.

## 2. API contract sau triển khai

`GET /api/public/properties/by-area?district=Gò%20Vấp&city=TP.%20Hồ%20Chí%20Minh`

Có thể truyền `city`, `district`, `ward`; cần ít nhất một giá trị khác rỗng. Các trường được kết hợp bằng AND. Không có radius, owner, booking, contract hoặc dữ liệu quản trị.

- `200 OK`: mảng JSON (mảng rỗng nếu không tìm thấy khu trọ).
- `400 Bad Request`: `ValidationProblemDetails` nếu thiếu toàn bộ tiêu chí hoặc tiêu chí không hợp lệ.
- Mỗi phần tử: `propertyId`, `propertyName`, `addressText`, `ward`, `district`, `city`, `latitude`, `longitude`, `primaryImageUrl`, `availableRoomCount`, `startingPrice`.
- Chỉ trả property có ít nhất một phòng trong tập `GetRentableRooms` tại thời điểm UTC của truy vấn. Số phòng là distinct room count và giá khởi điểm là giá thấp nhất từ chính tập phòng đó (`RoomTypes.Price`).
- Chuẩn hóa tên ở service/query layer: bỏ dấu tiếng Việt, chuẩn hóa `đ`, khoảng trắng, một số tiền tố hành chính và alias địa danh phổ biến. Không yêu cầu cài extension `unaccent` trong PostgreSQL.

Ví dụ response:

```json
[
  {
    "propertyId": 1,
    "propertyName": "Nhà trọ Bình An",
    "addressText": "12 Đường Số 5",
    "ward": "Phường 10",
    "district": "Gò Vấp",
    "city": "TP. Hồ Chí Minh",
    "latitude": 10.838,
    "longitude": 106.665,
    "primaryImageUrl": null,
    "availableRoomCount": 3,
    "startingPrice": 2500000
  }
]
```

Giá trị JSON ở ví dụ chỉ minh họa cấu trúc, không phải dữ liệu seed/live.

## 3. Triển khai

- BE thêm route, query/response DTO, validation/normalization và repository query. Projection chỉ lấy các trường public cần cho card. `AvailableRoomCount` và `StartingPrice` cùng dùng một predicate rentability để tránh lệch tập phòng.
- FE thêm typed API client, geolocation theo thao tác người dùng, reverse geocode, fallback area, map React Leaflet, marker dạng card ảnh/giá, popup có `Xem chi tiết` và `Chỉ đường`, cùng danh sách card đồng bộ trạng thái chọn.
- Mặc định tìm theo district (kèm city nếu có), không áp bán kính. Nếu district rỗng kết quả thì thử toàn city và hiển thị thông báo fallback; nếu reverse geocode chỉ có ward thì thử ward rồi city.
- Map và Leaflet được lazy-load sau khi có kết quả để không tải code bản đồ trong lần tải đầu.
- Directions ưu tiên tọa độ của property, địa chỉ đầy đủ là fallback. Property thiếu tọa độ vẫn có trong danh sách và có thông báo không thể đặt marker.
- Vị trí chỉ được yêu cầu sau khi người dùng bấm nút. Tọa độ được gửi tới dịch vụ reverse geocode; UI nói rõ ứng dụng không lưu tọa độ này.
- Đã đọc Supabase ở chế độ chỉ đọc. Bảng `Properties` có các tên khu vực mẫu với dấu: `Bình Thạnh / Phường 25`, `Gò Vấp / Phường 10`, `Thủ Đức / Phường Linh Trung`, đều thuộc `TP. Hồ Chí Minh` (mỗi nhóm hiện có một property row trong kết quả tổng hợp). Đây là kiểm tra dữ liệu địa chỉ, không khẳng định các row đó có phòng rentable; không có dữ liệu nào được ghi.

## 4. Test matrix

| Hạng mục | Kết quả |
|---|---|
| BE area API tests và Swagger route test | 5/5 passed |
| BE regression suite | 207 passed, 7 skipped, 0 failed (214 total) |
| FE Vitest | 58 passed / 58, 7 files |
| FE management contract tests | 6 passed / 6 |
| FE TypeScript | `npm run typecheck` passed |
| FE production build | `npm run build` passed |
| Diff whitespace check | `git diff --check` passed |

FE tests bao phủ API query/validation, geolocation thành công và lỗi quyền, reverse-geocode/API lỗi, district→city và ward→city fallback, kết quả rỗng, map/list selection sync, directions và trạng thái tải. Map Leaflet thực tế được cô lập bằng mock component trong jsdom; kiểm tra tương tác trên trình duyệt thật chưa chạy.

7 BE tests bị skip do cấu hình PostgreSQL fixture/integration không có trong môi trường test hiện tại; không có secret/connection string nào được in. Không xác nhận được E2E đang chạy qua FE → BE → PostgreSQL.

## 5. UX và lỗi được xử lý

- Chưa xin vị trí: lời giải thích và nút bắt đầu; không tự hỏi quyền lúc tải trang.
- Đang xử lý: xin vị trí, xác định khu vực, tải property, tải lazy map.
- Permission denied / không hỗ trợ geolocation: thông báo cụ thể và nút thử lại.
- Reverse geocode lỗi / không xác định được khu vực: thông báo lỗi và thử lại.
- API lỗi: thông báo lỗi và thử lại.
- Không có kết quả district/ward: tự fallback city khi có city, nêu rõ cấp kết quả. Không kết quả toàn city: trạng thái rỗng.
- Property không có tọa độ: vẫn hiện trong danh sách; không có marker/directions theo tọa độ.

## 6. Known limitations / production considerations

- Repository hiện chiếu toàn bộ property có phòng rentable rồi lọc tên khu vực trong memory để giữ normalization portable giữa providers. Đúng với fixture/dữ liệu hiện tại, nhưng cần chuyển sang cột normalized/index hoặc biểu thức query provider-specific nếu dữ liệu lớn.
- Reverse geocode hiện dùng Nominatim công cộng và map dùng OpenStreetMap standard tiles. Đây là dịch vụ cộng đồng, không có SLA cho production; cần cấu hình provider phù hợp, caching/proxy và tuân thủ attribution/rate limits khi tải tăng.
- Build pass nhưng còn cảnh báo Vite: entry chunk 545 KB (Leaflet đã tách thành chunk riêng 158.12 KB), cùng cảnh báo `__dirname`/JSON import từ `vite.config.ts`. Không chặn build.
- Chưa xác minh giao diện trong browser thật, chưa chạy Postgres fixture/E2E do thiếu cấu hình fixture trong môi trường.

Tham khảo chính sách dịch vụ: [Nominatim Usage Policy](https://operations.osmfoundation.org/policies/nominatim/) và [OpenStreetMap Tile Usage Policy](https://operations.osmfoundation.org/policies/tiles/).

## 7. Changed files

**Backend**

- `PRN232_BE/PRN232.Api/Controllers/PublicPropertiesController.cs`
- `PRN232_BE/PRN232.Application.Tests/PublicPropertyListingApiTests.cs`
- `PRN232_BE/PRN232.Application.Tests/PublicPropertyListingServiceTests.cs`
- `PRN232_BE/PRN232.Application/DTOs/PublicPropertyListingDto.cs`
- `PRN232_BE/PRN232.Application/Interfaces/IPublicPropertyListingRepository.cs`
- `PRN232_BE/PRN232.Application/Interfaces/IPublicPropertyListingService.cs`
- `PRN232_BE/PRN232.Application/Services/PublicPropertyListingService.cs`
- `PRN232_BE/PRN232.Application/Services/PublicAreaNameNormalizer.cs`
- `PRN232_BE/PRN232.Infrastructure/Repositories/PublicPropertyListingRepository.cs`

**Frontend**

- `PRN232_FE/package.json`
- `PRN232_FE/pnpm-lock.yaml`
- `PRN232_FE/src/api/publicBrowsingApi.ts`
- `PRN232_FE/src/components/home/MapSection.tsx`
- `PRN232_FE/src/components/home/AreaPropertyMap.tsx`
- `PRN232_FE/src/index.css`
- `PRN232_FE/src/utils/areaDiscovery.ts`
- `PRN232_FE/src/vite-env.d.ts`
- `PRN232_FE/tests/areaDiscovery.test.ts`
- `PRN232_FE/tests/mapSection.test.tsx`
- `PRN232_FE/tests/publicBrowsingApi.test.ts`
- `PRN232_FE/tests/publicHome.test.tsx`

## 8. Next steps

1. Chạy E2E trên PostgreSQL fixture đã migrate và browser thật, gồm grant/deny location, fallback, property không có tọa độ và mở directions.
2. Chọn reverse-geocode/tile provider có chính sách/SLA phù hợp trước khi production traffic tăng; cấu hình attribution và cache.
3. Khi cần, bổ sung lựa chọn khu vực thủ công và bộ lọc radius như tính năng tách biệt; sau đó cân nhắc marker clustering.
4. Nếu số property tăng lớn, thêm cách lọc khu vực đã normalize ở database/index để tránh materialize toàn bộ property rentable.

## 9. Phase-only patch

Patch để review: `PHASE_ONLY.patch` trong cùng thư mục. Patch gồm đúng các file liệt kê ở trên; báo cáo không được đưa vào patch. Không commit/push/deploy.
