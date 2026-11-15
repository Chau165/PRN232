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
