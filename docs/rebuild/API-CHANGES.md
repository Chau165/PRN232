# API-CHANGES — thay đổi hợp đồng API trong đợt rebuild

> Người đọc chính: **Antigravity (FE)**, Codex (test), thành viên phụ trách mobile.
> Claude (BE) ghi vào đây **trước hoặc ngay khi** đổi API. FE/mobile code theo file này, không cần đọc code BE.
> Quy ước vai trò và quy trình: xem `docs/rebuild/PLAN.md` mục 5.

Mục lục
- [C1 — Định dạng lỗi validate mới (áp dụng mọi endpoint)](#c1)
- [C2 — Quy tắc validate phòng và giá thuê](#c2)
- [C3 — Quy tắc validate khu trọ, thiết bị, giá dịch vụ](#c3)
- [C4 — Địa chỉ chỉ còn 2 cấp (việc 1.4)](#c4)
- C5 — Thông báo nhắc hạn hợp đồng (việc 3.4)
- C6 — Phòng: số người tối đa, tầng, mô tả (việc 2.1)
- C7 — Thiết bị: số lượng, giá trị bồi thường (việc 2.2)
- [Việc FE cần làm (task 1.5)](#fe)

---

<a id="c1"></a>
## C1 — Định dạng lỗi validate mới · ✅ đã có trên `Tan_rebuild` (việc 1.1)

Khi body request sai quy tắc, API trả **400** trước khi vào nghiệp vụ, với **tất cả** lỗi theo từng field:

```json
{
  "status": 400,
  "title": "Dữ liệu không hợp lệ.",
  "detail": "Diện tích phòng phải từ 8 đến 100 m².",
  "errors": {
    "areaSqm":  ["Diện tích phòng phải từ 8 đến 100 m²."],
    "roomCode": ["Mã phòng tối đa 20 ký tự."]
  }
}
```

- Key trong `errors` là **tên field JSON camelCase** đúng như FE gửi lên → gắn thẳng vào ô nhập cùng tên.
- `detail` = lỗi đầu tiên → màn hình chỉ hiện một câu vẫn chạy như cũ (không vỡ).
- FE: `apiRequest` (`src/api/apiClient.ts`) **đã** đưa `errors` vào `ApiError.validationErrors`. Không cần sửa apiClient.
- Các lỗi nghiệp vụ khác (403, 404, 409, trùng mã phòng...) giữ nguyên định dạng cũ `{ title, detail }`.

---

<a id="c2"></a>
## C2 — Phòng và giá thuê theo khu trọ · ✅ đã có (việc 1.2a)

**`POST /api/rooms`** body `{ propertyId, roomTypeId, roomCode, areaSqm }`
**`PUT /api/rooms/{id}`** body `{ roomTypeId, roomCode, areaSqm }`

| Field | Quy tắc | Câu báo lỗi (BE trả đúng chữ này) |
|---|---|---|
| `propertyId` | > 0 (chỉ khi tạo) | Vui lòng chọn khu trọ. |
| `roomTypeId` | > 0 | Vui lòng chọn loại phòng. |
| `roomCode` | bắt buộc (sau khi trim) | Mã phòng không được để trống. |
| | ≤ 20 ký tự | Mã phòng tối đa 20 ký tự. |
| | chỉ `A-Z a-z 0-9 - .` | Mã phòng chỉ gồm chữ không dấu, số, dấu gạch ngang hoặc dấu chấm (ví dụ: A-101). |
| `areaSqm` | 8 ≤ x ≤ 100 | Diện tích phòng phải từ 8 đến 100 m². |
| | tối đa 1 chữ số thập phân | Diện tích chỉ được có tối đa 1 chữ số thập phân. |

**`POST /api/properties/{propertyId}/room-types`** body `{ roomTypeId, monthlyRent }`
**`PUT /api/properties/{propertyId}/room-types/{roomTypeId}`** body `{ monthlyRent }`

| Field | Quy tắc | Câu báo lỗi |
|---|---|---|
| `roomTypeId` | > 0 (chỉ khi tạo) | Vui lòng chọn loại phòng. |
| `monthlyRent` | 500.000 ≤ x ≤ 50.000.000 | Giá thuê phải từ 500.000đ đến 50.000.000đ mỗi tháng. |
| | bội số 1.000 | Giá thuê phải là bội số của 1.000đ. |

---

<a id="c3"></a>
## C3 — Khu trọ, thiết bị, giá dịch vụ · ✅ đã có (việc 1.2b)

**`POST /api/properties`**, **`PUT /api/properties/{id}`** (multipart/form-data)

| Field form | Quy tắc | Câu báo lỗi |
|---|---|---|
| `propertyName` | bắt buộc | Tên khu trọ không được để trống. |
| | 3–150 ký tự | Tên khu trọ phải từ 3 đến 150 ký tự. |
| `addressText` (số nhà, tên đường) | bắt buộc | Số nhà, tên đường không được để trống. |
| | 5–255 ký tự | Số nhà, tên đường phải từ 5 đến 255 ký tự. |
| `description` | ≤ 2.000 ký tự | Mô tả tối đa 2000 ký tự. |

Tỉnh/phường: không đổi (BE vẫn kiểm tra theo danh mục như Châu đã làm).

**`POST /api/equipments`** body `{ roomId, name, installDate, warrantyExpiry, supplier, status }`
**`PUT /api/equipments/{id}`** body như trên, không có `roomId`

| Field | Quy tắc | Câu báo lỗi |
|---|---|---|
| `roomId` | > 0 (chỉ khi tạo) | Vui lòng chọn phòng. |
| `name` | bắt buộc, ≤ 200 ký tự | Tên thiết bị là bắt buộc và không quá 200 ký tự. |
| `supplier` | bắt buộc, ≤ 200 ký tự | Nhà cung cấp là bắt buộc và không quá 200 ký tự. |
| `installDate` | **mới:** không sau hôm nay (giờ Việt Nam) | Ngày lắp đặt không được ở tương lai. |
| `warrantyExpiry` | ≥ `installDate` | Hạn bảo hành không được trước ngày lắp đặt. |
| `status` | chỉ `"Operational"` | Trạng thái thiết bị không được hỗ trợ. |

**`POST /api/properties/{id}/service-rates`** body `{ serviceId, price, unitCode, billingBasis, effectiveFrom, effectiveTo? }`

| Field | Khi nào | Quy tắc | Câu báo lỗi |
|---|---|---|---|
| `serviceId` | luôn | > 0 | Vui lòng chọn dịch vụ. |
| `price` | luôn | > 0 (**mới:** trước đây 0 hợp lệ) | Đơn giá phải lớn hơn 0. |
| | luôn | số đồng nguyên | Đơn giá phải là số đồng nguyên. |
| | `unitCode = "kwh"` | ≤ 4.152 | Giá điện tối đa 4.152đ/kWh (bậc cao nhất của EVN đã gồm VAT). Chủ trọ thu cao hơn hóa đơn điện có thể bị phạt. |
| | `unitCode = "m3"` | ≤ 16.560 | Giá nước tối đa 16.560đ/m³ (bậc cao nhất của công ty cấp nước đã gồm VAT và phí môi trường). |
| | `room_month`, `person_month`, `vehicle_month` | ≤ 1.000.000 | Phí dịch vụ hằng tháng tối đa 1.000.000đ. |

Ba con số trần do BE cấu hình (`UtilityPriceLimits`) và **sẽ đổi** khi giá nhà nước đổi (ví dụ VAT điện từ 2027). FE **không** nên chặn cứng bằng số này; chỉ cần hiện lỗi BE trả về, hoặc hiện gợi ý "tối đa 4.152đ/kWh" bên cạnh ô nhập.

---

<a id="c4"></a>
## C4 — Địa chỉ chỉ còn 2 cấp · ✅ phần 1 đã có (việc 1.4), phần 2 ⏳

Nhóm đã chốt: **mọi địa chỉ trong hệ thống là địa chỉ sau sáp nhập** (Tỉnh/Thành → Xã/Phường/Đặc khu).

**Phần 1 — đã có:**
- `GET /api/locations/provinces` → `[{ provinceCode, provinceName }]` và `GET /api/locations/wards?provinceCode=79` → `[{ wardCode, wardName }]` nay **không cần đăng nhập** (trước đây chỉ Admin).
- `PropertyDto` (mọi API trả khu trọ cho Admin/Manager) có thêm `provinceCode`, `wardCode`. Cả 32 khu trọ trên DB Live đã được gán mã (migration `AddPropertyAdministrativeCodes`).
- `POST/PUT /api/properties` (multipart): form **không còn nhận** `ward`, `district`, `city` dạng chữ (gửi lên sẽ bị bỏ qua). Chỉ dùng `provinceCode` + `wardCode`.
  - Tạo: bắt buộc cả hai mã (như trước).
  - Sửa: gửi cả hai mã → lưu mã và tên chuẩn. **Không gửi mã** → giữ nguyên tỉnh/phường đang lưu, nhưng khi đó **không được đổi** `addressText`.
- Việc FE: form sửa khu trọ **chọn sẵn** tỉnh/phường từ `provinceCode`/`wardCode` của khu trọ (thay vì đoán từ tên), và **luôn gửi** hai mã. Ngừng gửi `ward`/`district`/`city`.

**Phần 2 — chưa làm (cần nhóm xác nhận trước khi xóa cột):** bỏ hẳn field `district` khỏi mọi response (cả API công khai `/api/public/...`) và xóa cột `District` trong DB. FE nên **ngừng hiển thị** `district` ngay từ bây giờ.

---

## C5 — Thông báo nhắc hạn hợp đồng · ✅ đã có (việc 3.4)

Không có endpoint mới. Một job nền mỗi giờ tạo thông báo (đi qua `GET /api/notifications` và SignalR như mọi thông báo khác) cho **khách thuê và quản lý của khu trọ**:

| Khi nào | `title` |
|---|---|
| Còn 8–30 ngày | Hợp đồng phòng A101 sẽ hết hạn ngày 05/12/2026 (còn dưới 30 ngày) |
| Còn 0–7 ngày | Hợp đồng phòng A101 sẽ hết hạn ngày 20/11/2026 (còn dưới 7 ngày) |
| Đã quá ngày kết thúc, hợp đồng vẫn Active | Hợp đồng phòng A101 đã hết hạn ngày 12/11/2026 nhưng chưa gia hạn hoặc trả phòng |

- `type = "Contract"`, `relatedEntityId = contractId` → FE có thể bấm vào thông báo để mở chi tiết hợp đồng.
- Trạng thái hợp đồng **không đổi** (vẫn `Active`); `displayStatus` vẫn là `ExpiredPendingCheckout` khi quá hạn như trước.

---

## C6 — Phòng: số người tối đa, tầng, mô tả · ✅ đã có (việc 2.1)

Migration `AddRoomOccupancyFloorAndDescription`. Phòng đã có được điền sẵn `maxOccupants` = diện tích ÷ 8, làm tròn xuống, trong khoảng 1–10.

**Response** (thêm field, không bỏ field nào):
- `RoomDto` (`GET /api/rooms`, `/api/rooms/{id}`) và `PublicRoomDetailDto` (`GET /api/public/rooms/{id}`) có thêm: `maxOccupants: number`, `floor: number | null`, `description: string | null`.

**Request** `POST /api/rooms`, `PUT /api/rooms/{id}` thêm 3 field **không bắt buộc**:

| Field | Quy tắc | Câu báo lỗi |
|---|---|---|
| `maxOccupants` | 1 ≤ x ≤ 10 | Số người tối đa phải từ 1 đến 10. |
| | x ≤ làm tròn xuống(`areaSqm` ÷ 8) | Phòng 20 m² chỉ được ở tối đa 2 người (tối thiểu 8 m²/người). |
| `floor` | 0 ≤ x ≤ 50 (0 = tầng trệt) | Tầng phải từ 0 (tầng trệt) đến 50. |
| `description` | ≤ 1.000 ký tự | Mô tả phòng tối đa 1000 ký tự. |

Ý nghĩa khi **không gửi** (null):
- `maxOccupants` khi tạo: lấy mức tối đa theo diện tích. Khi sửa: **giữ** giá trị đang lưu (tự giảm nếu diện tích mới không còn đủ).
- `floor`, `description`: null = không có giá trị (gửi null khi sửa sẽ **xóa** giá trị cũ). FE nên luôn gửi đủ 3 field khi sửa.

Căn cứ 8 m²/người: Luật Nhà ở 2023 + Nghị định 100/2024 + Thông tư 05/2024 (dãy nhà trọ) và Luật Cư trú 2020; xem PLAN.md mục D2.

Việc FE gợi ý: trong `RoomFormModal` thêm 3 ô; ô số người dùng `min=1`, `max = min(10, floor(diện tích/8))`, cập nhật khi đổi diện tích và hiện gợi ý "Tối đa N người cho phòng X m²". Trang chi tiết phòng (quản lý và công khai) hiển thị số người tối đa, tầng ("Tầng trệt" khi 0), mô tả.

---

## C7 — Thiết bị: số lượng, giá trị bồi thường · ✅ đã có (việc 2.2)

Migration `AddEquipmentQuantityAndCompensationValue`. Thiết bị đã có: `quantity = 1`, `compensationValue = 0` (chưa định giá, Admin/Manager cập nhật dần).

**Response** `EquipmentDto` (`GET /api/equipments`, `/api/equipments/{id}`) thêm: `quantity: number`, `compensationValue: number`.

**Request** `POST /api/equipments`, `PUT /api/equipments/{id}` thêm 2 field **không bắt buộc**:

| Field | Quy tắc | Câu báo lỗi |
|---|---|---|
| `quantity` | 1 ≤ x ≤ 20 | Số lượng phải từ 1 đến 20. |
| `compensationValue` (đ, **mỗi cái**) | 0 ≤ x ≤ 50.000.000 | Giá trị bồi thường phải từ 0đ đến 50.000.000đ. |
| | bội số 1.000 | Giá trị bồi thường phải là bội số của 1.000đ. |

Không gửi (null): tạo mới thì `1` cái, `0đ`; sửa thì **giữ** giá trị đang lưu. Mobile (`equipment_service.dart`) không cần sửa gấp.

Ý nghĩa `compensationValue`: giá trị ghi trong biên bản bàn giao, là **mức trần** khi quyết toán cọc. Khách chỉ bồi thường hư hỏng do mình gây ra, **không** bồi thường hao mòn tự nhiên (BLDS 2015, Điều 479). FE nên ghi chú điều này cạnh ô nhập, ví dụ: "Giá trị ghi trong biên bản bàn giao; không áp dụng cho hao mòn tự nhiên".

Việc FE gợi ý: thêm 2 ô vào form thiết bị; danh sách thiết bị hiển thị "Máy lạnh × 2 — 8.500.000đ/cái".

---

## C8 — Chi tiết phòng công khai hiển thị thiết bị · ✅ đã có (việc 2.4)

Nhóm đã chốt: khách thuê cần biết phòng có gì. `GET /api/public/rooms/{id}` trước đây luôn trả `equipments: []`, giờ trả thiết bị thật của phòng:

```json
"equipments": [{ "equipmentId": 1, "name": "Máy lạnh", "quantity": 2 }]
```

Chỉ có 3 field này. Nhà cung cấp, bảo hành, trạng thái, giá trị bồi thường **không** công khai (thông tin nội bộ của chủ trọ). Không cần đăng nhập, không đổi URL.

Việc FE gợi ý: type `PublicRoomDetail.equipments` thêm `quantity: number`; `RoomDetailModal` (và trang chi tiết phòng nếu có) hiển thị mục "Thiết bị trong phòng", ví dụ "Máy lạnh × 2" (ẩn "× 1"). Không có thiết bị thì ẩn mục.

---

## C9 — Đặt cọc: ngày dọn vào, số người ở · ✅ đã có (việc 3.1)

Migration `AddBookingMoveInDateAndOccupantCount` (2 cột cho phép NULL; booking cũ để NULL).

**Request** `POST /api/bookings` thêm 2 field, **tạm thời không bắt buộc** để FE cũ và mobile không hỏng:

```json
{ "roomId": 12, "viewingId": 3, "moveInDate": "2026-11-20", "occupantCount": 2 }
```

| Field | Quy tắc | Câu báo lỗi | Key trong `errors` |
|---|---|---|---|
| `moveInDate` (`yyyy-MM-dd`) | ≥ hôm nay (giờ VN) | Ngày dọn vào không được ở quá khứ. | `moveInDate` |
| | ≤ hôm nay + 14 ngày | Ngày dọn vào tối đa 14 ngày kể từ hôm nay. | `moveInDate` |
| `occupantCount` | 1 ≤ x ≤ 10 | Số người ở phải từ 1 đến 10. | `occupantCount` |
| | ≤ `maxOccupants` của phòng | Phòng {roomCode} chỉ được ở tối đa {N} người. | `occupantCount` |

Không gửi: `moveInDate` = hôm nay, `occupantCount` = 1. Khi FE và mobile đã gửi đủ, BE sẽ chuyển 2 field này sang **bắt buộc** (sẽ báo trước).

**Response** `BookingDto` (tạo, xem, danh sách) thêm `moveInDate: string | null`, `occupantCount: number | null` (null ở booking cũ).

Chưa đổi trong việc này: thời hạn giữ phòng vẫn là 7 ngày sau khi cọc (`holdUntilUtc`). Việc 3.2 sẽ đổi thành "ngày dọn vào + 3 ngày".

Việc FE gợi ý: form đặt cọc thêm ô chọn ngày (`min` = hôm nay, `max` = hôm nay + 14) và ô số người (`min=1`, `max` = `maxOccupants` lấy từ chi tiết phòng công khai, C6). Trang danh sách/chi tiết booking hiển thị "Dọn vào 20/11/2026 · 2 người".

---

## C10 — Hủy cọc, hạn giữ phòng, khóa hoàn cọc qua hệ thống · ✅ đã có (việc 3.2a)

Migration `AddBookingCancellationReason` (2 cột NULL: `CancellationReason`, `CancelledAtUtc`).

**Nhóm đã chốt:** hệ thống **không hoàn tiền qua tài khoản**. Khoản phải hoàn do quản lý trả trực tiếp cho khách; khách xem thông tin liên hệ trong booking.

### Luật hủy (`POST /api/bookings/{id}/cancel`, không đổi URL/body)

| Ai hủy | Khi nào | `cancellationReason` | Tiền cọc |
|---|---|---|---|
| Khách | Chưa cọc hoặc **đã cọc** (trước đây bị chặn, giờ được phép) | `CustomerCancelled` | Đã cọc thì **mất cọc** |
| Quản lý / chủ trọ | Còn trong hạn giữ phòng (`canReleaseRoom = false`) | `LandlordCancelled` | **Hoàn 100%** ngoài hệ thống (`depositRefundDue = true`) |
| Quản lý / chủ trọ | Hết hạn giữ phòng (`canReleaseRoom = true`) | `NoShow` | Mất cọc |

**Hạn giữ phòng** (`holdUntilUtc`): đến **hết ngày "ngày dọn vào + 3"** theo giờ VN. Ví dụ dọn vào 20/11 → giữ đến hết 23/11 (`holdUntilUtc` = `2026-11-23T17:00:00Z`). Booking cũ không có `moveInDate`: vẫn 7 ngày kể từ lúc cọc.

### Response `BookingDto` thêm

| Field | Ý nghĩa |
|---|---|
| `cancellationReason` | `CustomerCancelled` / `LandlordCancelled` / `NoShow` / `null` (booking hủy trước rebuild hoặc chưa hủy) |
| `cancelledAtUtc` | Thời điểm hủy |
| `depositRefundDue` | `true` = chủ trọ hủy sau khi khách đã cọc → phải hoàn 100% ngoài hệ thống |
| `contacts` | `[{ fullName, phoneNumber, email, role }]` — quản lý đang phụ trách khu trọ (`role = "Manager"`); khu chưa có quản lý thì là chủ trọ (`role = "Owner"`) |

`canRequestRefund` giờ **luôn `false`**. `POST /api/bookings/{id}/refund-requests` trả **409** với `detail` = "Hệ thống không hoàn tiền cọc qua tài khoản. Vui lòng liên hệ trực tiếp quản lý khu trọ (xem mục Liên hệ trong booking)." Các API xem lịch sử hoàn cọc cũ và trang duyệt của Admin vẫn chạy (cho dữ liệu cũ). Code hoàn cọc payOS **giữ nguyên**, bật lại bằng `BookingRefunds:RequestsEnabled = true`.

### Việc FE gợi ý
1. **Ẩn** nút "Xin hoàn cọc" và form nhập tài khoản ngân hàng (`BookingRefundPanel`); vẫn cho xem đơn hoàn cọc cũ nếu `refundStatus` khác null. **Không xóa** code — chỉ ẩn.
2. **Khách** (`MyBookingsPage`): booking đã cọc có nút "Hủy đặt cọc", bấm thì hiện hộp xác nhận: *"Nếu hủy, bạn sẽ mất tiền cọc {depositAmount}. Bạn chắc chắn?"*
3. **Quản lý** (`ManagementBookingsPage`): booking đã cọc, chưa có hợp đồng:
   - `canReleaseRoom = false` → nút "Hủy (hoàn 100% cọc)", xác nhận: *"Khách sẽ được hoàn {depositAmount}. Bạn cần tự chuyển tiền cho khách."*
   - `canReleaseRoom = true` → nút "Nhả phòng (khách mất cọc)" như hiện tại.
   - Hiển thị hạn giữ phòng: "Giữ phòng đến hết {holdUntilUtc − 1 giây, giờ VN, dd/MM/yyyy}".
4. Booking đã hủy hiển thị lý do. Nếu `depositRefundDue = true`: khách thấy khung **"Bạn được hoàn 100% tiền cọc {depositAmount}. Liên hệ để nhận lại tiền:"** + danh sách `contacts` (tên, "Gọi/Zalo: {phoneNumber}" dạng link `tel:`, email dạng `mailto:`).
5. Menu "Hoàn cọc" của Admin: giữ (cho đơn cũ) hoặc ẩn nếu không còn đơn nào — nhóm chọn.
6. **Đặt cọc từ lịch xem phòng** (`MyBookingsPage.tsx`, hàm `bookSelectedRoom`) đang gọi `createBooking` chỉ với `roomId` + `viewingId`, nên BE mặc định dọn vào **hôm nay** → phòng chỉ được giữ 3 ngày. Cần cho khách chọn ngày dọn vào và số người như `DepositPage` (gợi ý: chuyển sang `DepositPage?roomId=..&viewingId=..` dùng chung form C9).
7. **Điều khoản cọc phải hiện TRƯỚC khi khách giữ phòng** (`DepositPage`, bước giữ phòng). Pháp luật không quy định số ngày; điều khoản chỉ có giá trị khi khách được biết trước. Thay câu "phòng được giữ 7 ngày" bằng, ví dụ: *"Phòng được giữ đến hết ngày {ngày dọn vào + 3}. Nếu bạn hủy hoặc không đến nhận phòng trước hạn đó, tiền cọc không được hoàn. Nếu chủ trọ hủy, bạn được hoàn 100% (liên hệ trực tiếp quản lý để nhận lại tiền)."* Ngày tính lại theo ô ngày dọn vào khách đang chọn.
8. Thay các câu cũ còn ghi "7 ngày": `ManagementBookingsPage.tsx` (hộp xác nhận nhả phòng), `MyBookingsPage.tsx` ("7 ngày kể từ khi đặt cọc"). Giờ hạn giữ phòng lấy từ `holdUntilUtc`.
9. Nhỏ: khi ô ngày dọn vào bị xóa trống, câu lỗi đang là "không được ở quá khứ" → nên là "Vui lòng chọn ngày dọn vào."

Chưa đổi trong C10: câu "Tiền cọc được trừ vào tiền phòng tháng đầu tiên" vẫn đúng cho đến việc 3.3 (cọc thành tiền đảm bảo, không trừ tháng đầu).

---

## C11 — Quản lý xác nhận "Đã hoàn cọc" · ✅ đã có (việc 3.2b)

Migration `AddBookingDepositReturned` (2 cột NULL + khóa ngoại tới `Users`).

Hệ thống **không chuyển tiền**. Quản lý trả tiền cho khách bên ngoài (chuyển khoản, tiền mặt…), rồi bấm nút để hệ thống lưu vết và báo cho khách.

**`POST /api/bookings/{id}/deposit-returned`** — không có body. Quyền: **Manager** đang phụ trách khu trọ hoặc **Admin là chủ** khu trọ.

| Kết quả | Khi nào |
|---|---|
| 200 + `BookingDto` | Booking có `depositRefundDue = true` |
| 409 | Không có khoản phải hoàn (khách tự hủy, không đến nhận phòng, chưa hủy) hoặc **đã xác nhận rồi** (bấm 2 lần) |
| 403 | Khách, quản lý khu khác, Admin không phải chủ |

Sau khi xác nhận: `depositRefundDue = false`, thêm `depositReturnedAtUtc` và `depositReturnedByName`. Khách nhận thông báo: *"Booking #12 - Đã xác nhận hoàn 3.000.000đ tiền cọc phòng A101"* kèm câu "Nếu bạn chưa nhận được, hãy liên hệ quản lý khu trọ".

### Việc FE gợi ý (`ManagementBookingsPage`)
1. Booking có `depositRefundDue = true`: nhãn **"Chờ hoàn cọc {depositAmount}"** + nút **"Đã hoàn cọc"**.
2. Bấm nút → **cửa sổ xác nhận** (không dùng `window.confirm`):
   - Tiêu đề: "Xác nhận đã hoàn cọc"
   - Nội dung: "Khách **{customerName}** ({customerPhone}) · Booking #{bookingId} · Phòng {roomCode}" và số tiền lớn, rõ: **"{depositAmount}"**.
   - Ghi chú: "Chỉ xác nhận sau khi bạn ĐÃ chuyển tiền cho khách. Hệ thống không chuyển tiền."
   - Hai nút: **"Xác nhận đã hoàn"** (gọi API) và **"Hủy"** (đóng cửa sổ, không gọi API).
3. Sau khi xác nhận: thay nhãn bằng "Đã hoàn cọc lúc {depositReturnedAtUtc} — {depositReturnedByName}".
4. Phía khách (`MyBookingsPage`): khung liên hệ ở C10 mục 4 đổi thành "Quản lý đã xác nhận hoàn {depositAmount} lúc …" khi `depositReturnedAtUtc` khác null.

---

## C12 — Thông báo mới về đặt cọc · ✅ đã có (việc 3.2c)

Không đổi API, không có migration. Thông báo đi qua hệ thống thông báo sẵn có (`Type = "Booking"`, `relatedEntityId = bookingId`), FE không cần sửa gì.

| Khi nào | Gửi cho | Tiêu đề (ví dụ dọn vào 20/11) |
|---|---|---|
| Lần chạy đầu tiên trong khoảng 20/11–23/11 | Khách | Ngày dọn vào phòng A101: 20/11/2026 |
| như trên | Quản lý đang phụ trách (khu chưa có quản lý: chủ trọ) | Khách Nguyễn Văn A dọn vào phòng A101 ngày 20/11/2026 |
| Ngày 23/11 (ngày cuối giữ phòng) | Khách | Phòng A101 chỉ được giữ đến hết ngày 23/11/2026 |
| Khách tự hủy booking đã cọc | Quản lý đang phụ trách (hoặc chủ trọ) | Booking #12 - Khách Nguyễn Văn A đã hủy đặt cọc phòng A101 |

Job nền chạy mỗi giờ; mỗi thông báo chỉ gửi một lần. Tiêu đề ghi ngày cụ thể nên nếu server tắt đúng ngày dọn vào, lần chạy sau vẫn gửi bù đúng nội dung.

Việc FE nhỏ (gom khi rảnh): `DepositPage` bước thanh toán, dòng "Giữ phòng đến" đang hiện **hạn thanh toán 15 phút** (`expiryDate`) → đổi nhãn thành "Hạn thanh toán cọc".

---

## C13 — Cọc thành tiền đảm bảo, không trừ vào tháng đầu · ✅ đã có (việc 3.3)

Migration `AddBookingDepositTerms`: cột `Bookings.DepositAppliesToFirstMonth`. **Mọi booking đã có = `true`** (giữ đúng lời hứa "trừ vào tháng đầu" khách đã đọc lúc cọc). Booking mới lấy theo cấu hình `BookingDeposits:AppliesToFirstMonth` (mặc định `false` = luật mới).

| | Luật cũ (`true`) | Luật mới (`false`) |
|---|---|---|
| Lúc cọc | 1 tháng tiền phòng | 1 tháng tiền phòng |
| Hóa đơn tháng đầu | Có dòng âm `PrepaidRentCredit` trừ cọc | **Đủ tiền phòng**, không có dòng trừ |
| Cọc trong hợp đồng | `prepaidRentAmount` | `securityDepositAmount` — giữ đến khi trả phòng (quyết toán ở việc 3.6) |

**Response thêm:**
- `BookingDto.depositAppliesToFirstMonth: boolean` — điều khoản của **booking đó**.
- `ContractDto.securityDepositAmount: number` — tiền cọc đang giữ (0 với hợp đồng luật cũ).

Hóa đơn đã phát hành **không đổi**.

### Việc FE gợi ý
1. **`DepositPage`, bước giữ phòng** — thay câu "Tiền cọc được trừ vào tiền phòng tháng đầu tiên khi lập hợp đồng" bằng: *"Tiền cọc được giữ làm tiền đảm bảo trong suốt thời gian thuê và hoàn lại khi trả phòng, sau khi trừ tiền phòng còn nợ và hư hỏng thiết bị (nếu có). Tháng đầu tiên bạn đóng đủ tiền phòng."* Giữ phần hạn giữ phòng / mất cọc của C10.
2. Chỗ nào đang nói "cọc trừ tháng đầu" cho một booking cụ thể (`MyBookingsPage`, `CreateContractPage`…): hiển thị theo `booking.depositAppliesToFirstMonth` — `true` giữ câu cũ, `false` dùng câu mới.
3. Chi tiết hợp đồng (quản lý + khách): nếu `securityDepositAmount > 0` hiện dòng **"Tiền cọc (hoàn lại khi trả phòng): {securityDepositAmount}"**; nếu `prepaidRentAmount > 0` giữ cách hiển thị cũ.

### Thứ tự deploy (quan trọng) — C13
Câu điều khoản trên FE phải khớp với điều khoản BE ghi vào booking:
- **Deploy BE + FE cùng lúc**: để `BookingDeposits:AppliesToFirstMonth = false` (mặc định).
- **Deploy BE trước FE**: đặt biến môi trường `BookingDeposits__AppliesToFirstMonth = true` trên Railway (BE vẫn chạy luật cũ, khớp câu FE cũ). Khi FE có câu mới đã lên → đổi thành `false` (không cần deploy lại).

Nút "Đã hoàn cọc": xem C11.

---

<a id="fe"></a>
## Việc FE cần làm (task 1.5, Antigravity)

Hiện 4 trang đang **gộp hết lỗi thành một câu** (`Object.values(cause.validationErrors ?? {}).flat()`):
`ManagementPropertiesPage.tsx:105`, `ManagementPropertyRoomTypesPage.tsx:40`, `ManagementRoomEquipmentPage.tsx:41`, `ManagementRoomsPage.tsx:68`.

1. **Hiện lỗi dưới đúng ô nhập** theo key trong `validationErrors` (bảng C2, C3). Lỗi không gắn được với ô nào thì hiện `message` như hiện tại.
2. **Chặn sớm phía client** bằng đúng quy tắc và câu báo lỗi trong C2, C3 (trừ trần giá điện nước, xem ghi chú C3). Mẫu có sẵn: `src/utils/accountValidation.ts` đang làm đúng việc này cho tài khoản. Gợi ý tạo `src/utils/roomValidation.ts`, `propertyValidation.ts`, `equipmentValidation.ts`.
3. Sửa thuộc tính ô nhập cho khớp: diện tích `min="8" max="100" step="0.1"` (hiện `RoomFormModal` đang `min="0.01"`); giá thuê `min="500000" max="50000000" step="1000"`; ngày lắp đặt `max={hôm nay}`.
4. Màn hình liên quan: `RoomFormModal.tsx`, `PropertyFormModal.tsx`, `ManagementPropertyRoomTypesPage.tsx`, `ManagementRoomEquipmentPage.tsx`, `PropertyServiceRatesPanel.tsx`.
5. Test: thêm/cập nhật Vitest cho từng form (giá trị tại biên: 8 qua, 7.9 lỗi; 500.000 qua, 499.000 lỗi).

Phạm vi: chỉ sửa trong `PRN232_FE/src/**` và `PRN232_FE/tests/**`. Không sửa BE.

### Ảnh hưởng mobile (`PRN232_Mobile`)
- `lib/core/services/equipment_service.dart` tạo/sửa thiết bị → giờ có thể nhận 400 với `errors` theo field, và ngày lắp đặt ở tương lai bị chặn. Cần kiểm tra mobile hiện `detail` thay vì lỗi chung chung.
- Các endpoint tài khoản (đăng ký, đổi mật khẩu, sửa hồ sơ) giờ cũng trả thêm `errors`; `detail` giữ nguyên nên mobile không vỡ.

---

## C14 — Biên bản bàn giao thiết bị (nhận phòng / trả phòng) · ✅ đã có (việc 3.5a)

Migration `AddHandoverRecords` (2 bảng mới `HandoverRecords`, `HandoverItems`; không đổi bảng cũ). Ảnh/PDF minh chứng: việc 3.5b (sắp có).

Mỗi hợp đồng có tối đa **1 biên bản nhận phòng** và **1 biên bản trả phòng**. Mỗi biên bản ghi **đủ mọi thiết bị hiện có của phòng**, mỗi thiết bị một tình trạng. Tên, số lượng, giá trị bồi thường do BE **chụp lại** từ danh sách thiết bị (FE không gửi) — sửa thiết bị sau này không đổi biên bản. Biên bản trả phòng dùng giá trị đã chụp lúc **nhận phòng** (mức hai bên đã thống nhất).

### `GET /api/contracts/{contractId}/handovers`
Quyền: khách của hợp đồng, quản lý đang phụ trách khu trọ, Admin.

```json
{
  "contractId": 12,
  "moveIn":  { "handoverRecordId": 1, "kind": "MoveIn", "handoverDate": "2026-11-20", "note": "...",
               "recordedByName": "Manager A", "recordedAtUtc": "...", "totalCharge": 0,
               "items": [ { "handoverItemId": 1, "equipmentId": 501, "equipmentName": "Máy lạnh", "quantity": 1,
                            "compensationValue": 8500000, "condition": "Good", "chargeAmount": 0,
                            "note": null, "moveInCondition": null } ] },
  "moveOut": null,
  "roomEquipments": [ { "equipmentId": 501, "name": "Máy lạnh", "quantity": 1, "compensationValue": 8500000 } ]
}
```
`moveIn`/`moveOut` = `null` khi chưa lập. Ở `moveOut.items`, `moveInCondition` là tình trạng lúc nhận phòng (để so sánh).

### `PUT /api/contracts/{contractId}/handovers/move-in` và `/move-out`
Quyền: **chỉ Manager đang phụ trách**. Lưu lần nữa = thay toàn bộ biên bản đó. Trả về cùng dạng như GET.

```json
{ "handoverDate": "2026-11-20", "note": "Biên bản có chữ ký hai bên",
  "items": [ { "equipmentId": 501, "condition": "Damaged", "chargeAmount": 1200000, "note": "Vỡ mặt nạ" } ] }
```

| `condition` | Hiển thị | Được tính tiền (chỉ ở trả phòng) |
|---|---|---|
| `Good` | Tốt | Không |
| `Worn` | Hao mòn | **Không** (hao mòn tự nhiên — BLDS 2015 Điều 479) |
| `Damaged` | Hư hỏng | Có |
| `Missing` | Mất | Có |

| Lỗi | Key trong `errors` | Câu báo lỗi |
|---|---|---|
| Ngày ở tương lai | `handoverDate` | Ngày bàn giao không được ở tương lai. |
| Tình trạng sai | `items[i].condition` | Tình trạng phải là Tốt, Hao mòn, Hư hỏng hoặc Mất. |
| Tiền âm / lẻ | `items[i].chargeAmount` | Tiền bồi thường không được âm. / …phải là bội số của 1.000đ. |
| Tính tiền ở biên bản nhận phòng | `items[i].chargeAmount` | Biên bản nhận phòng không tính tiền bồi thường. |
| Tính tiền thiết bị Tốt/Hao mòn | `items[i].chargeAmount` | {tên}: chỉ tính tiền khi thiết bị hư hỏng hoặc mất (hao mòn tự nhiên không phải bồi thường). |
| Thiết bị đã hỏng/mất từ lúc nhận phòng | `items[i].chargeAmount` | {tên} đã hư hỏng hoặc mất từ lúc nhận phòng nên không tính tiền khách. |
| Vượt trần | `items[i].chargeAmount` | {tên}: tiền bồi thường tối đa {giá × SL}đ ({SL} × {giá}đ). |
| Thiếu thiết bị | (`detail`) | Biên bản phải ghi đủ thiết bị của phòng, còn thiếu: {tên,…}. |
| Ngày trước ngày bắt đầu HĐ / trả trước nhận | (`detail`) | 400 |
| Sửa biên bản nhận phòng khi đã có biên bản trả phòng | — | 409 |

### Việc FE gợi ý
1. Chi tiết hợp đồng (quản lý + khách): mục **"Biên bản bàn giao"** với 2 thẻ Nhận phòng / Trả phòng.
2. **Quản lý**: nút "Lập biên bản nhận phòng" / "Lập biên bản trả phòng" → form một dòng cho mỗi `roomEquipments` (tên × SL, giá trị), dropdown tình trạng (Tốt / Hao mòn / Hư hỏng / Mất), ghi chú. Ở **trả phòng**: hiện cột "Lúc nhận phòng: {moveInCondition}", ô "Tiền bồi thường" chỉ bật khi chọn Hư hỏng/Mất **và** lúc nhận phòng không phải Hư hỏng/Mất, gợi ý tối đa = SL × giá. Dòng tổng "Tổng bồi thường: {totalCharge}".
3. **Khách**: chỉ xem (không có nút sửa).
4. Hiện lỗi theo dòng bằng key `items[i].field`.

---

## C15 — Ảnh/PDF minh chứng: nhận phòng, trả phòng, hợp đồng đã ký · ✅ đã có (việc 3.5b + 3.5c)

Migration `AddContractEvidenceFiles` (1 bảng mới). File lưu trong bucket **private** `contract-evidence`; người xem chỉ nhận **link tạm 1 giờ** (`url`) — **không lưu `url` lâu dài**, mỗi lần mở trang thì gọi lại GET.

Nhóm đã chốt: khách **và** quản lý cùng upload, **không có bước duyệt**. Mỗi người chỉ xóa file của mình. Admin chỉ **ẩn** (kèm lý do), không xóa.

| `kind` | Ý nghĩa | Upload / xóa được khi |
|---|---|---|
| `MoveIn` | Ảnh tình trạng lúc nhận phòng + ảnh biên bản giấy đã ký | HĐ đang hiệu lực **và** chưa quá **7 ngày** kể từ ngày nhận phòng (`moveInUploadDeadline`) |
| `MoveOut` | Ảnh lúc trả phòng + biên bản trả phòng đã ký | HĐ đang hiệu lực hoặc đã kết thúc (việc 3.6 sẽ khóa khi đã quyết toán cọc) |
| `SignedContract` | Ảnh/scan hợp đồng giấy đã ký | HĐ đang hiệu lực hoặc đã kết thúc |

Định dạng: **JPG / PNG / WEBP ≤ 5 MB**, **PDF ≤ 10 MB**; BE kiểm tra nội dung thật của file. Tối đa **20 file mỗi loại** mỗi hợp đồng.

### API
| | Quyền | |
|---|---|---|
| `GET /api/contracts/{id}/evidence` | Khách của HĐ, quản lý phụ trách, Admin | Danh sách + quyền upload |
| `POST /api/contracts/{id}/evidence?kind=MoveIn` | Khách, quản lý | `multipart/form-data`, field **`file`**, **mỗi lần 1 file** |
| `DELETE /api/contracts/{id}/evidence/{fileId}` | Người đã upload | |
| `POST /api/contracts/{id}/evidence/{fileId}/hide` | Admin | body `{ "reason": "..." }` (bắt buộc, ≤ 500 ký tự) |

Mọi API đều trả về toàn bộ danh sách mới:

```json
{
  "contractId": 12,
  "moveInUploadDeadline": "2026-11-27",
  "canUploadMoveIn": true, "canUploadMoveOut": true, "canUploadSignedContract": true,
  "files": [ { "contractEvidenceFileId": 3, "kind": "MoveIn", "fileName": "vet-nut-tuong.png",
               "contentType": "image/png", "sizeBytes": 812345, "uploadedByName": "Nguyễn Văn A",
               "uploadedByRole": "Customer", "uploadedAtUtc": "...", "canDelete": true,
               "url": "https://…/storage/v1/object/sign/…?token=…", "isHidden": false, "hiddenReason": null } ]
}
```
File bị ẩn: `isHidden = true`, `url = null` (Admin vẫn thấy `url`).

| Lỗi | HTTP | `detail` |
|---|---|---|
| Sai định dạng / quá dung lượng / file giả đuôi | 400 | Chỉ nhận ảnh JPG, PNG, WEBP hoặc file PDF hợp lệ. / Ảnh tối đa 5 MB, PDF tối đa 10 MB. |
| Quá hạn ảnh nhận phòng | 409 | Đã hết hạn upload hoặc xóa ảnh nhận phòng (hạn đến hết ngày dd/MM/yyyy). |
| Đủ 20 file | 409 | Mỗi loại minh chứng tối đa 20 file. |
| Xóa file người khác | 403 | |
| Admin upload | 403 | |

### Việc FE gợi ý
1. Chi tiết hợp đồng (khách + quản lý): 3 khu **"Ảnh nhận phòng"**, **"Ảnh trả phòng"**, **"Hợp đồng đã ký"** — lưới ảnh thu nhỏ (PDF hiện biểu tượng + tên file), bấm để xem lớn / mở tab mới. Mỗi ảnh ghi "{uploadedByName} · {uploadedAtUtc}".
2. Nút "Thêm ảnh" (cho chọn nhiều file, FE gửi **lần lượt** từng file, hiện tiến độ) chỉ hiện khi `canUploadX = true`. Kiểm tra trước ở FE: loại file và dung lượng như bảng trên.
3. Khu ảnh nhận phòng ghi rõ: **"Bạn có thể thêm hoặc xóa ảnh nhận phòng đến hết ngày {moveInUploadDeadline}"**.
4. Nút xóa chỉ hiện khi `canDelete = true`, có hộp xác nhận.
5. Admin: nút "Ẩn" mở hộp nhập lý do. File đã ẩn hiện "Đã ẩn: {hiddenReason}" (không có ảnh).

---

## C16 — Báo trả phòng (30 ngày) · ✅ đã có (việc 3.6a)

Migration `AddContractMoveOutNotice` (3 cột NULL trên `Contracts`).

Luật nhóm đã chốt: trả phòng **trước ngày kết thúc hợp đồng** mà **báo trước dưới 30 ngày** → khi quyết toán (3.6b) **mất phần cọc còn lại** sau khi trừ bồi thường. Trả đúng ngày kết thúc thì không cần báo trước. **Mỗi lần đổi ngày** thì số ngày báo trước tính lại từ lúc báo mới.

| API | Quyền | Body |
|---|---|---|
| `PUT /api/contracts/{id}/move-out-notice` | Khách của HĐ, quản lý phụ trách (nhập giúp) | `{ "plannedMoveOutDate": "2026-12-20" }` |
| `DELETE /api/contracts/{id}/move-out-notice` | như trên | — (hủy báo, HĐ tiếp tục như cũ) |

Trả về `ContractDto`, thêm 4 field:

| Field | Ý nghĩa |
|---|---|
| `plannedMoveOutDate` | Ngày dự kiến trả phòng (`null` = chưa báo) |
| `moveOutNoticeAtUtc` | Lúc báo |
| `moveOutNoticeDays` | Số ngày từ lúc báo đến ngày trả (giờ VN) |
| `moveOutForfeitsDeposit` | `true` = trả trước hạn mà báo dưới 30 ngày → **sẽ mất cọc** |

Lỗi: ngày ở quá khứ → 400; sau ngày kết thúc HĐ → 400 ("…Muốn ở lâu hơn hãy gia hạn hợp đồng."); HĐ không còn hiệu lực / hủy khi chưa báo → 409; người ngoài, Admin → 403.

Thông báo: khách báo → các quản lý phụ trách nhận "Báo trả phòng A101 ngày dd/MM/yyyy" (kèm cảnh báo mất cọc nếu có); quản lý nhập giúp → khách nhận thông báo.

### Việc FE gợi ý
1. Chi tiết hợp đồng (khách + quản lý, HĐ đang hiệu lực): nút **"Báo trả phòng"** → chọn ngày (`min` = hôm nay, `max` = `endDate`).
2. **Trước khi gửi**, FE tự tính và cảnh báo đỏ nếu ngày chọn < `endDate` và cách hôm nay < 30 ngày: *"Bạn trả phòng trước hạn hợp đồng mà báo trước dưới 30 ngày nên sẽ mất phần tiền cọc còn lại khi quyết toán. Báo muộn nhất ngày X để giữ cọc."* (X = ngày chọn − 30).
3. Đã báo: hiện "Dự kiến trả phòng {plannedMoveOutDate} · báo trước {moveOutNoticeDays} ngày" + nhãn đỏ nếu `moveOutForfeitsDeposit`, nút "Đổi ngày" và "Hủy báo trả phòng".

---

## C17 — Quyết toán cọc khi trả phòng · ✅ đã có (việc 3.6b)

Migration `AddDepositSettlements` (1 bảng mới). Chỉ áp dụng cho hợp đồng có `securityDepositAmount > 0` (điều khoản mới, C13). Hợp đồng luật cũ: GET trả 409 "không có tiền cọc đảm bảo" và trả phòng như trước.

### Thứ tự bắt buộc
1. **Biên bản trả phòng** (C14) — ngày trả phòng và tiền bồi thường lấy từ đây.
2. **Quyết toán cọc** (API dưới) — khóa biên bản trả phòng và ảnh trả phòng.
3. **Xác nhận trả phòng** (`POST /api/contracts/{id}/terminate` cũ) — nếu HĐ có cọc đảm bảo mà chưa quyết toán → **409** "Cần lập biên bản trả phòng và quyết toán cọc trước khi xác nhận trả phòng."

### Công thức (nhóm chốt phương án B)
Cọc **C** dùng theo thứ tự: (1) bù bồi thường **B**; (2) giữ bù nợ hóa đơn **D** nếu khách còn nợ; (3) phần còn lại: **mất** nếu trả trước ngày kết thúc HĐ mà báo trước dưới 30 ngày (C16), ngược lại **hoàn** cho khách. B > C thì khách **nộp thêm** B − C.

| Ví dụ (C = 3.000.000) | Hoàn | Mất | Giữ bù nợ | Nộp thêm |
|---|---|---|---|---|
| B = 1,2tr, báo trước đủ 30 ngày | 1.800.000 | 0 | 0 | 0 |
| B = 1,2tr, trả sớm không báo đủ | 0 | 1.800.000 | 0 | 0 |
| B = 4tr | 0 | 0 | 0 | 1.000.000 |
| B = 0, còn nợ 500k | 2.500.000 | 0 | 500.000 | 0 |

Bình thường khách **trả hết hóa đơn trước** khi quyết toán. "Giữ bù nợ" chỉ dành cho khách bỏ đi còn nợ. **Hạn chế đã biết:** hóa đơn vẫn hiện đủ số nợ (hệ thống chưa có thanh toán một phần); nếu phần giữ lại đủ trả trọn một hóa đơn, quản lý bấm "Ghi nhận tiền mặt" cho hóa đơn đó.

### API
| | Quyền | |
|---|---|---|
| `GET /api/contracts/{id}/settlement` | Khách của HĐ, quản lý phụ trách, Admin | Chưa quyết toán → **bản xem trước** (`isSettled: false`); chưa có biên bản trả phòng → 409 "Cần lập biên bản trả phòng trước khi quyết toán cọc." |
| `POST /api/contracts/{id}/settlement` | Quản lý phụ trách | body `{ "outstandingDebtAcknowledged": false, "note": "..." }` |

```json
{ "contractId": 12, "isSettled": true, "moveOutDate": "2026-11-15", "contractEndDate": "2027-11-15",
  "depositAmount": 3000000, "damageCharges": 1200000, "damageCoveredByDeposit": 1200000,
  "outstandingDebt": 0, "retainedForDebt": 0, "forfeitedAmount": 0, "refundAmount": 1800000, "amountDue": 0,
  "earlyWithoutNotice": false, "noticeDays": 40, "note": "...", "settledByName": "Manager A", "settledAtUtc": "...",
  "refundConfirmedAtUtc": null, "refundConfirmedByName": null, "dueCollectedAtUtc": null, "dueCollectedByName": null }
```

| Lỗi POST | HTTP |
|---|---|
| Khách còn nợ mà chưa xác nhận | 400 — "Khách còn nợ hóa đơn {X}đ. Nên thu hết trước khi quyết toán; nếu khách không trả, hãy xác nhận để giữ lại cọc bù nợ." |
| Đã quyết toán / HĐ đã kết thúc / chưa có biên bản trả phòng / HĐ luật cũ | 409 |
| Không phải quản lý phụ trách | 403 |

Khách nhận thông báo "Quyết toán cọc phòng A101": *"Cọc 3.000.000đ: bồi thường thiết bị 1.200.000đ; hoàn lại 1.800.000đ. Quản lý sẽ chuyển trả trực tiếp cho bạn (hệ thống không chuyển tiền)."*

### Việc FE gợi ý
1. Chi tiết hợp đồng có cọc đảm bảo: mục **"Quyết toán cọc"** (khách + quản lý) hiện bảng: Tiền cọc / Bồi thường thiết bị / Giữ bù nợ / Mất cọc (kèm lý do "trả trước hạn, báo trước {noticeDays} ngày < 30") / **Hoàn lại** / **Khách nộp thêm**.
2. Quản lý, chưa quyết toán: bảng xem trước + nút **"Xác nhận quyết toán"** → hộp xác nhận nhắc số tiền hoàn/nộp thêm. Nếu `outstandingDebt > 0`: cảnh báo + ô tick "Khách không trả, giữ cọc bù nợ".
3. Nút "Trả phòng" (terminate): với HĐ có `securityDepositAmount > 0`, chỉ bật khi đã quyết toán; hướng dẫn thứ tự 3 bước ở trên.
4. Sau quyết toán: ẩn nút sửa biên bản trả phòng và thêm ảnh trả phòng.
5. Nút "Đã hoàn cọc" / "Đã thu": xem C18.

---

## C18 — Xác nhận "Đã hoàn cọc" / "Đã thu" sau quyết toán · ✅ đã có (việc 3.6c)

Không có migration (cột đã thêm ở C17). Giống nút "Đã hoàn cọc" của booking (C11): hệ thống **không chuyển tiền**, quản lý trả/nhận tiền bên ngoài rồi bấm xác nhận để lưu vết và báo khách.

| API | Khi nào dùng | Quyền |
|---|---|---|
| `POST /api/contracts/{id}/settlement/refund-confirmed` | `refundAmount > 0` và `refundConfirmedAtUtc = null` | Quản lý phụ trách, hoặc Admin là **chủ** khu trọ |
| `POST /api/contracts/{id}/settlement/due-collected` | `amountDue > 0` và `dueCollectedAtUtc = null` | như trên |

Không có body. Trả về `DepositSettlementDto` (C17) với `refundConfirmedAtUtc`/`refundConfirmedByName` hoặc `dueCollectedAtUtc`/`dueCollectedByName` đã điền. Không có khoản đó / đã xác nhận rồi (bấm 2 lần) / chưa quyết toán → **409**; khách, quản lý khu khác, Admin không phải chủ → **403**.

Khách nhận thông báo: *"Đã xác nhận hoàn 1.800.000đ tiền cọc phòng A101"* (kèm "Nếu bạn chưa nhận được, hãy liên hệ quản lý khu trọ") hoặc *"Đã xác nhận thu 1.000.000đ tiền bồi thường phòng A101"*.

### Việc FE gợi ý (mục "Quyết toán cọc", C17)
1. `refundAmount > 0`: nhãn **"Chờ hoàn {refundAmount}"** + nút **"Đã hoàn cọc"** → cửa sổ xác nhận giống C11 (tên khách, SĐT, số tiền lớn, ghi chú "Chỉ xác nhận sau khi bạn ĐÃ chuyển tiền cho khách. Hệ thống không chuyển tiền.", nút "Xác nhận đã hoàn" / "Hủy"). Sau đó: "Đã hoàn lúc … — {refundConfirmedByName}".
2. `amountDue > 0`: nhãn **"Khách cần nộp thêm {amountDue}"** + nút **"Đã thu"** → cửa sổ tương tự ("Chỉ xác nhận sau khi bạn ĐÃ nhận tiền của khách."). Sau đó: "Đã thu lúc … — {dueCollectedByName}".
3. Phía khách: hiện trạng thái tương ứng ("Quản lý đã xác nhận hoàn … lúc …" / "Chờ quản lý hoàn …").

---

## C19 — Nhật ký thao tác (audit log) · ✅ đã có (việc 5.1, lớp 1)

Migration `AddAuditLogs` (1 bảng mới). Theo PLAN D7.

**Ghi tự động** mọi request `POST/PUT/PATCH/DELETE` (kể cả thất bại: 400, 404, 409, 500): ai (`userId`, `role`), thao tác (`action` = `"{Controller}.{Action}"`, vd. `Bookings.Cancel`), đường dẫn, đối tượng (`entityType` = controller, `entityId` = id trên đường dẫn hoặc id vừa tạo), mã HTTP, IP, `correlationId`. **Không lưu nội dung request/response** (mật khẩu, CCCD, tài khoản ngân hàng không bao giờ vào nhật ký). Không ghi: request bị chặn quyền (401/403 trước khi vào action), `POST /api/invoices/preview`, đánh dấu thông báo đã đọc. Lỗi ghi nhật ký không làm hỏng thao tác của người dùng.

Lớp 2 (việc 5.2, sắp có): `oldValues`/`newValues` (JSON) cho nghiệp vụ quan trọng (đặt cọc, hủy, ký HĐ, quyết toán, trả phòng, đổi giá).

### `GET /api/audit-logs` — chỉ Admin
Query (đều không bắt buộc): `userId`, `entityType` (vd. `Bookings`, `Contracts`, `Invoices`, `Rooms`…), `entityId`, `succeeded` (`true`/`false`), `from`, `to` (`yyyy-MM-dd`, giờ VN, gồm cả 2 đầu, tối đa 366 ngày), `page` (≥ 1), `pageSize` (1–100, mặc định 50). Mới nhất trước.

```json
{ "items": [ { "auditLogId": 1201, "createdAtUtc": "2026-11-15T03:12:44Z", "userId": 10, "userName": "Nguyễn Văn A",
               "role": "Customer", "action": "Bookings.Cancel", "httpMethod": "POST", "path": "/api/bookings/12/cancel",
               "entityType": "Bookings", "entityId": "12", "statusCode": 200, "succeeded": true,
               "ipAddress": "203.0.113.5", "correlationId": "0HN…", "oldValues": null, "newValues": null } ],
  "totalCount": 1, "page": 1, "pageSize": 50 }
```
Bộ lọc sai (`pageSize` > 100, `to` < `from`…) → 400. Không phải Admin → 403.

### Việc FE gợi ý (task 5.3)
Trang Admin **"Nhật ký thao tác"** (sidebar nhóm Hệ thống): bảng Thời gian (giờ VN) / Người thực hiện (tên + vai trò) / Thao tác / Đối tượng (`entityType #entityId`) / Kết quả (nhãn xanh "Thành công" hoặc đỏ "Thất bại {statusCode}") / IP. Bộ lọc: người dùng, loại đối tượng (dropdown), mã đối tượng, kết quả, khoảng ngày; phân trang. Bấm một dòng mở chi tiết (đường dẫn, correlationId, và oldValues/newValues dạng JSON khi có). Nên có bảng dịch `action` sang tiếng Việt cho các thao tác chính (vd. `Bookings.Cancel` → "Hủy đặt cọc", `DepositSettlements.Settle` → "Quyết toán cọc").
