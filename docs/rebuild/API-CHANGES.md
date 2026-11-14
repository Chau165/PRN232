# API-CHANGES — thay đổi hợp đồng API trong đợt rebuild

> Người đọc chính: **Antigravity (FE)**, Codex (test), thành viên phụ trách mobile.
> Claude (BE) ghi vào đây **trước hoặc ngay khi** đổi API. FE/mobile code theo file này, không cần đọc code BE.
> Quy ước vai trò và quy trình: xem `docs/rebuild/PLAN.md` mục 5.

Mục lục
- [C1 — Định dạng lỗi validate mới (áp dụng mọi endpoint)](#c1)
- [C2 — Quy tắc validate phòng và giá thuê](#c2)
- [C3 — Quy tắc validate khu trọ, thiết bị, giá dịch vụ](#c3)
- [C4 — Sắp tới: địa chỉ chỉ còn 2 cấp (việc 1.4, chưa làm)](#c4)
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
## C4 — Sắp tới: địa chỉ chỉ còn 2 cấp · ⏳ chưa làm (việc 1.4)

Nhóm đã chốt: **mọi địa chỉ trong hệ thống là địa chỉ sau sáp nhập** (Tỉnh/Thành → Xã/Phường/Đặc khu). Dự kiến thay đổi, **chưa** có trên BE:
- `GET /api/locations/provinces` và `/wards` mở cho **mọi người** (không cần đăng nhập), để trang tìm phòng dùng chung danh mục.
- `PropertyDto` trả thêm `provinceCode`, `wardCode`; bỏ `district`.
- Tạo và sửa khu trọ **đều** bắt buộc `provinceCode` + `wardCode`; form sửa không còn nhận `ward`/`district`/`city` dạng chữ.

FE chưa cần làm gì cho C4. Khi BE xong, mục này sẽ đổi thành ✅ kèm chi tiết.

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
