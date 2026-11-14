# Kế hoạch Rebuild nghiệp vụ — Hệ thống Quản lý Phòng Trọ (PRN232)

> Lập ngày 14/11/2026, sau buổi review với giảng viên. Cả nhóm đã đồng ý rebuild.
> File này là **nguồn sự thật chung** cho cả nhóm và cho 3 AI (Claude, Antigravity, Codex).
> Mọi thay đổi nghiệp vụ phải cập nhật vào đây trước khi code.
>
> **Bản 2 (14/11/2026):** đã đối chiếu với `main` sau PR #32 của Châu (giá phòng theo khu trọ, địa chỉ 2 cấp, luồng xem phòng mới). Các mục bị ảnh hưởng được đánh dấu **[cập nhật theo main]**.

---

## 0. Đọc nhanh

| # | Vấn đề | Mức độ | Workstream |
|---|--------|--------|------------|
| 1 | **Tiền cọc đang bị trừ vào tiền phòng tháng đầu** → suốt hợp đồng chủ trọ không giữ đồng cọc nào, trả phòng không có gì để quyết toán | 🔴 Nghiêm trọng | WS3 |
| 2 | Hợp đồng chỉ có **1 người** (`Contracts.CustomerId`), không có người ở cùng → không xử lý được ở ghép, chuyển đi | 🔴 Nghiêm trọng | WS4 |
| 3 | Phòng không có **số người tối đa**; dịch vụ tính theo đầu người bị chặn "not supported yet" | 🔴 Nghiêm trọng | WS2, WS4 |
| 4 | Validate gần như chỉ "không rỗng" và "> 0" (diện tích 99.999 m², giá phòng 0đ vẫn hợp lệ) | 🟠 Cao | WS1 |
| 5 | ~~Địa chỉ 3 cấp nhập tay~~ → **đã làm phần lớn trên main** (dropdown Tỉnh/Phường, danh mục NSO). Còn thiếu: chưa lưu mã tỉnh/phường, đường cập nhật còn nhận `District` cũ | 🟡 Còn dọn | WS1 |
| 6 | Không có **audit log** (ai làm gì, lúc nào) | 🟠 Cao | WS5 |
| 7 | Trả phòng (`TerminateContract`) không quyết toán cọc, không kiểm kê thiết bị | 🟠 Cao | WS3 |
| 8 | Hợp đồng hết hạn không tự chuyển trạng thái, không nhắc trước | 🟡 Trung bình | WS3 |
| 9 | Chi tiết phòng / khu trọ hiển thị khác nhau giữa các vai trò; sidebar 13 mục phẳng, không nhóm | 🟡 Trung bình | WS6 |
| 10 | Ảnh **đã** lưu trên Supabase Storage (không phải DB) — chỉ cần làm rõ khi demo | 🟢 Đã có | WS6 |
| 11 | **Ứng dụng mobile** (`PRN232_Mobile`, Flutter) gọi cùng API; mọi thay đổi API/luồng đặt cọc đều có thể làm vỡ mobile | 🟠 Cao | Tất cả |
| 12 | Migration của Châu (`PropertyScopedRoomTypesAndViewingFlow`) **chưa chạy trên DB Live**, các migration của rebuild phải xếp sau | 🟠 Cao | Điều phối |

---

## 1. Hiện trạng (kết quả rà soát code)

### 1.1 Validate & dữ liệu đầu vào **[cập nhật theo main]**
- `RoomService` chỉ kiểm tra `RoomCode` không rỗng, `AreaSqm > 0`, trùng mã phòng.
- `PropertyRoomTypeService` (mới) chỉ kiểm tra giá thuê `> 0` → **giá 1đ vẫn hợp lệ**. Migration của Châu chỉ đánh dấu cấu hình cũ là "ngừng dùng" khi giá `< 10.000`, nhưng đó chỉ là xử lý dữ liệu cũ, không phải quy tắc nhập mới.
- `RoomTypeService` (danh mục loại phòng): tên không rỗng, `Price >= 0`. Cột này giờ là dữ liệu legacy.
- `PropertyService` đã kiểm tra tỉnh/phường theo danh mục (mới), nhưng tên khu trọ và địa chỉ vẫn chỉ cần không rỗng.
- FluentValidation có trong tech stack nhưng chỉ dùng cho tài khoản (`Validation/AccountValidators.cs`).
- Không có giới hạn độ dài chuỗi ở phần lớn DTO.

### 1.2 Mô hình phòng **[cập nhật theo main]**
- `Rooms`: `RoomCode`, `AreaSqm`, `Status`, `RoomTypeId`. **Không có** `MaxOccupants`, tầng, mô tả.
- Giá nay nằm ở `PropertyRoomTypes.MonthlyRent` (mỗi khu trọ có giá riêng cho từng loại phòng, khóa `PropertyId + RoomTypeId`). `Rooms` tham chiếu cặp này. `RoomTypes.Price` còn trong schema nhưng không dùng cho hiển thị.
- `Bookings.AgreedMonthlyRent` lưu giá thuê đã chốt lúc đặt, hợp đồng lấy từ đây. Booking cũ không có giá chốt (ví dụ Booking 4) sẽ bị chặn tạo hợp đồng cho tới khi có người đối soát.
- `Equipments`: tên, ngày lắp, bảo hành, nhà cung cấp, trạng thái. **Không có** giá trị bồi thường, số lượng, tình trạng lúc bàn giao. Trang thiết bị nay là **theo từng phòng** (`ManagementRoomEquipmentPage`).

### 1.3 Luồng đặt cọc → hợp đồng (luồng chính) **[cập nhật theo main]**
Luồng hiện tại (đã có thêm luồng xem phòng mới của Châu ở đầu):
```
Khách xin xem phòng theo (khu trọ + loại phòng) → quản lý chọn phòng cụ thể → duyệt lịch
  → (tùy chọn) đặt cọc từ lịch xem đã duyệt (Booking.ViewingId), giá chốt vào AgreedMonthlyRent
Khách tạo Booking (Pending, giữ 15 phút)
  → Thanh toán cọc = 1 tháng tiền phòng (Deposited)
  → Giữ phòng 7 ngày (DepositHoldPolicy)
      ├─ Quá 7 ngày chưa ký HĐ → quản lý nhả phòng, khách MẤT cọc
      ├─ Khách xin hoàn trước khi ký → BookingRefundRequest → payout PayOS
      └─ Quản lý tạo hợp đồng từ booking
           → Hóa đơn tháng đầu: dòng "PrepaidRentCredit" TRỪ toàn bộ cọc vào tiền phòng
           → Trả phòng: Terminate (chỉ ghi nhận công nợ, KHÔNG quyết toán cọc)
```
Vấn đề:
- Cọc biến thành **tiền phòng trả trước** → không còn cọc đảm bảo trong suốt hợp đồng. Thực tế: cọc được giữ đến khi trả phòng, rồi trừ nợ và hư hỏng mới hoàn lại.
- Khách không chọn được **ngày dự kiến dọn vào** và **số người ở**.
- Không phân biệt "khách bỏ cọc" và "chủ trọ từ chối" (theo Điều 328 BLDS 2015 hai trường hợp này xử lý khác nhau).
- Không có thời hạn báo trước khi trả phòng sớm.

### 1.4 Hóa đơn & điện nước
- Dòng hóa đơn hỗ trợ: tiền phòng, điện (kWh), nước (m³), dịch vụ cố định theo phòng.
- Dịch vụ theo **người** (`person_month/occupant`) hoặc **xe** đã cấu hình được giá nhưng lập hóa đơn thì ném lỗi "not supported yet" (`InvoiceService.BuildServiceLine`).
- Không có trần giá điện/nước, không cảnh báo chỉ số tăng bất thường.

### 1.5 Log
- Có `ILogger` (log kỹ thuật). **Không có bảng audit log.**
- Lưu ý kỹ thuật: repository dùng rất nhiều `ExecuteUpdateAsync`/`ExecuteSqlRaw` (~65 chỗ) → các thao tác này **không đi qua** `SaveChanges`, nên interceptor EF Core một mình sẽ bỏ sót.

### 1.6 Ảnh
- Ảnh phòng, khu trọ, báo hỏng đã upload lên **Supabase Storage** (`Infrastructure/Storage/*`), DB chỉ lưu URL, có giới hạn 5 MiB và kiểm tra JPEG/PNG/WebP, có xóa file khi xóa ảnh. → Hướng này đúng, **giữ nguyên**.

---

## 2. Quyết định nghiệp vụ đề xuất

> Các mục có 🔒 cần **cả nhóm chốt** trước khi code workstream đó. Con số in nghiêng là đề xuất, nhóm có thể đổi.

### D1. Địa chỉ 2 cấp — **phần lớn đã xong trên main (Châu)** **[cập nhật theo main]**
- Từ 1/7/2025 cấp huyện chấm dứt hoạt động; cả nước còn **34 tỉnh/thành** và **3.321 đơn vị cấp xã** (Nghị quyết 202/2025/QH15).
- Đã có: danh mục NSO dạng file JSON (`AdministrativeDivisionCatalog`), `GET /api/locations/provinces` và `/wards`, form tạo khu trọ dùng dropdown Tỉnh → Phường, kiểm tra phường thuộc đúng tỉnh.
- **Không làm lại** bảng `Provinces`/`Wards` như bản 1 của plan. Dùng tiếp danh mục JSON của Châu.
- Còn thiếu (chỉ là việc dọn, không phải làm mới):
  - `Property` chưa lưu `ProvinceCode`/`WardCode`, chỉ lưu tên vào `Ward`/`City`. Nên lưu mã để tìm kiếm và thống kê không phụ thuộc cách viết tên.
  - Cột `District` vẫn còn (tạo mới ghi chuỗi rỗng) và `UpdatePropertyDto` vẫn nhận `Ward`/`District`/`City` cũ cho khu trọ đời cũ. Cần quyết định có migrate hết khu trọ cũ sang mã rồi bỏ cột hay không.
  - Endpoint `/api/locations` đang giới hạn `Admin`; nếu khách cũng cần chọn khu vực khi tìm phòng thì phải mở public.

### D2. Mô hình phòng và validate 🔒
| Trường | Quy tắc đề xuất | Lý do |
|--------|-----------------|-------|
| `AreaSqm` | *8 – 100 m²*, tối đa 1 chữ số thập phân | Phòng trọ thực tế; chặn 99.999 m² |
| `MaxOccupants` (mới) | *1 – 10*, và diện tích ≥ *5 m²/người* | Theo đề xuất của Sở Xây dựng TP.HCM (chưa phải luật — dùng làm quy tắc của hệ thống) |
| `RoomCode` | 1–20 ký tự, chữ/số/`-`/`.`, duy nhất trong khu | |
| `PropertyRoomTypes.MonthlyRent` **[đổi đối tượng]** | *500.000 – 50.000.000đ*, bội số 1.000 | Hiện chỉ chặn `<= 0`. Quy tắc này thay cho `RoomTypes.Price` (legacy) |
| `PropertyName` | 3–150 ký tự | |
| `AddressText` | 5–255 ký tự | |
| `Description` | ≤ 2.000 ký tự | |
| Số điện thoại | 10 số, đầu số VN | |
| Thời hạn hợp đồng | *1 – 36 tháng*, ngày bắt đầu trong *30 ngày* tới | |
- Toàn bộ validate chuyển sang **FluentValidation**, trả `400 ValidationProblem` có lỗi theo từng field → FE hiện lỗi dưới đúng ô nhập.
- FE dùng **dropdown** cho mọi danh mục (tỉnh, xã, loại phòng, trạng thái, dịch vụ).

### D3. Luồng đặt cọc mới (luồng chính) 🔒
```
1. (Tùy chọn) Đặt lịch xem phòng
2. Đặt cọc giữ chỗ
   - Khách chọn: ngày dự kiến dọn vào (trong 14 ngày), số người ở (≤ MaxOccupants)
   - Thanh toán qua payOS trong 15 phút, nếu không booking tự hết hạn
   - Phòng chuyển Reserved đến ngày dọn vào + 3 ngày ân hạn
3. Trước khi ký hợp đồng
   - Khách hủy           → mất cọc        (Điều 328 BLDS 2015)
   - Chủ trọ từ chối/huỷ → hoàn 100% cọc (luật cho phép phạt thêm 1 khoản bằng cọc — nhóm chọn có áp dụng không)
   - Quá hạn dọn vào     → quản lý nhả phòng, khách mất cọc
4. Ký hợp đồng
   - Cọc giữ chỗ CHUYỂN THÀNH cọc hợp đồng (tiền cọc đảm bảo), giữ nguyên số tiền
   - Hóa đơn đầu tiên = tiền phòng tháng đầu, KHÔNG trừ cọc
   - Ghi người đứng tên + danh sách người ở cùng, kiểm kê thiết bị, chốt chỉ số điện nước
5. Trong hợp đồng: hóa đơn hằng tháng, gia hạn, nhắc trước 30 ngày khi sắp hết hạn
6. Trả phòng → QUYẾT TOÁN CỌC
   Hoàn lại = Cọc − Nợ hóa đơn − Bồi thường thiết bị − (phạt trả sớm nếu có)
   - Đúng hạn hoặc báo trước ≥ 30 ngày → hoàn theo công thức trên
   - Trả sớm không báo trước           → mất cọc (theo điều khoản hợp đồng)
   - Số âm → tạo hóa đơn công nợ cuối kỳ
```
- Bỏ dòng `PrepaidRentCredit`. Các hợp đồng cũ đã bị trừ cọc thì giữ nguyên, chỉ áp dụng luật mới cho hợp đồng mới.
- Thêm trạng thái hợp đồng `Expired` và job nền tự chuyển trạng thái, gửi thông báo trước 30 ngày và 7 ngày.

### D4. Người ở cùng và chuyển đi trước hạn 🔒
Mô hình: **1 người đứng tên** (chịu trách nhiệm tiền phòng, cọc) + bảng `ContractOccupants` (họ tên, CCCD, SĐT, ngày vào, ngày ra).

| Tình huống | Xử lý |
|------------|-------|
| Người **ở cùng** chuyển đi | Ghi ngày ra. Tiền phòng giữ nguyên (tính theo phòng). Dịch vụ theo đầu người giảm từ tháng sau. Cọc không đổi — chia lại cọc giữa các bạn ở chung là chuyện riêng, hệ thống không can thiệp. |
| Người **đứng tên** chuyển đi, người ở cùng ở tiếp | **Chuyển người đứng tên**: kết thúc HĐ cũ và tạo HĐ mới cho người ở lại. Chỉ số điện nước nối tiếp. Cọc: hoặc **chuyển sang** HĐ mới (cả hai xác nhận), hoặc quyết toán cho người cũ rồi người mới đóng cọc mới. |
| Cả phòng chuyển đi trước hạn | Luồng trả phòng sớm ở D3 bước 6 |
| Thêm người mới vào ở | Chỉ cho phép khi tổng số người ≤ `MaxOccupants` |

Lợi ích thêm: biết chính xác số người đang ở → tính được dịch vụ theo đầu người và hiển thị số người để chủ trọ **đăng ký định mức điện** với EVN (thực tế cứ 4 người được tính là 1 hộ; không kê khai thì toàn bộ sản lượng bị tính giá bậc 3).

### D5. Điện nước và hạn mức 🔒
- Bật dịch vụ tính **theo người** dựa trên `ContractOccupants` (kể cả tính theo tỷ lệ ngày khi vào/ra giữa tháng).
- Cấu hình **trần giá điện và nước** cho mỗi khu trọ. Chặn giá cao hơn trần, vì chủ trọ thu tiền điện cao hơn giá quy định có thể bị phạt 7–10 triệu đồng. Biểu giá EVN thay đổi theo thời kỳ nên trần để trong cấu hình, nhóm tra biểu giá hiện hành rồi điền.
- **Cảnh báo** (không chặn) khi chỉ số tháng này vượt *X%* so với trung bình 3 tháng gần nhất.

### D6. Thiết bị và hóa đơn ("nằm chung – không tách")
Cách hiểu đề xuất (nên **hỏi lại giảng viên**):
1. **Nghiệp vụ**: thiết bị là danh mục **kiểm kê của phòng**, không phải khoản tiền hằng tháng. Thêm `Quantity`, `CompensationValue`, `Condition`. Biên bản bàn giao lúc nhận phòng và lúc trả phòng (`ContractHandoverItems`). Hư hỏng do khách gây ra → khoản bồi thường trong **quyết toán cọc**, không lẫn vào hóa đơn tháng.
2. **Giao diện**: sidebar đang để "Hóa đơn" và "Thiết bị" cạnh nhau trong danh sách phẳng 13 mục. Nhóm lại theo nghiệp vụ (xem WS6).

### D7. Audit log
- Bảng `AuditLogs`: `Id, UserId, Role, Action, EntityType, EntityId, OldValues(json), NewValues(json), IpAddress, CorrelationId, CreatedAtUtc`.
- **Hai lớp**, vì EF interceptor bỏ sót các lệnh `ExecuteUpdate`:
  1. **Action filter** cho mọi request `POST/PUT/PATCH/DELETE`: tự ghi ai, endpoint nào, kết quả → phủ 100% ngay.
  2. **Ghi chủ động ở service** cho nghiệp vụ quan trọng (đặt cọc, hủy, ký HĐ, trả phòng, quyết toán, đổi giá), có dữ liệu trước/sau.
- Không bao giờ ghi `PasswordHash`, CCCD, token.
- Trang Admin: lọc theo người, loại đối tượng, khoảng thời gian.

### D8. Ảnh
Giữ Supabase Storage. Bổ sung:
- Xóa file trên Storage khi xóa phòng hoặc khu trọ.
- Khi present, nói rõ: "ảnh lưu trên object storage, DB chỉ lưu URL".

---

## 3. Workstreams và công việc

> **Owner**: `Claude` = BE/nghiệp vụ · `AG` = Antigravity (FE/UI) · `Codex` = test + review.
> Cột **Người** để nhóm trưởng điền thành viên chịu trách nhiệm (người review, demo, trả lời giảng viên).
> **Size**: S ≈ ½ ngày · M ≈ 1–2 ngày · L ≈ 3+ ngày.

### WS1 — Validate & địa chỉ (nền tảng)
| ID | Việc | Owner | Phụ thuộc | Size | Người |
|----|------|-------|-----------|------|-------|
| 1.1 | Khung FluentValidation + format lỗi `ValidationProblem` thống nhất | Claude | — | S | |
| 1.2 | Validator cho Property, Room, **PropertyRoomType**, Equipment, Service rate theo bảng D2 | Claude | 1.1 | M | |
| ~~1.3~~ | ~~Bảng Provinces/Wards~~ — **Châu đã làm** (danh mục JSON + `/api/locations`). Đã bỏ khỏi plan | — | — | — | Châu |
| 1.4 | Dọn địa chỉ: lưu `ProvinceCode`/`WardCode` vào `Property`, quyết định số phận cột `District`, mở `/api/locations` cho khách nếu cần | Claude | hỏi Châu | S | |
| 1.5 | FE: phần form **phòng** và **giá theo khu trọ** dùng dropdown, hiện lỗi theo field (form khu trọ đã có dropdown) | AG | 1.1 | S | |
| 1.6 | Test validate biên (đúng biên, dưới, trên) cho từng DTO | Codex | 1.2 | M | |

### WS2 — Mô hình phòng
| ID | Việc | Owner | Phụ thuộc | Size | Người |
|----|------|-------|-----------|------|-------|
| 2.1 | Thêm `MaxOccupants`, `Floor`, `Description` vào phòng | Claude | 1.2 | S | |
| 2.2 | Thiết bị: `Quantity`, `CompensationValue`, `Condition` | Claude | — | S | |
| 2.3 | FE: form phòng và thiết bị mới | AG | 2.1, 2.2 | S | |

### WS3 — Đặt cọc, hợp đồng, quyết toán (LUỒNG CHÍNH)
| ID | Việc | Owner | Phụ thuộc | Size | Người |
|----|------|-------|-----------|------|-------|
| 3.1 | Booking thêm `MoveInDate`, `OccupantCount`, validate ≤ `MaxOccupants` (giữ nguyên `AgreedMonthlyRent` và `ViewingId` của Châu) | Claude | 2.1 | M | |
| 3.2 | Chính sách hủy theo D3 bước 3 (khách hủy / chủ hủy / quá hạn dọn vào) | Claude | 3.1 | M | |
| 3.3 | Cọc chuyển thành cọc hợp đồng, bỏ `PrepaidRentCredit` cho HĐ mới | Claude | 3.1 | M | |
| 3.4 | Trạng thái `Expired` + job nhắc hạn 30 và 7 ngày | Claude | — | S | |
| 3.5 | Biên bản bàn giao thiết bị lúc nhận phòng và trả phòng | Claude | 2.2 | M | |
| 3.6 | **Quyết toán cọc** khi trả phòng (công thức D3 bước 6), báo trước 30 ngày | Claude | 3.3, 3.5 | L | |
| 3.7 | FE: trang đặt cọc mới, trang trả phòng và quyết toán | AG | 3.1, 3.6 | L | |
| 3.8 | Test end-to-end toàn luồng: cọc → ký → hóa đơn → trả phòng → hoàn cọc | Codex | 3.6 | L | |
| 3.9 | Mobile (Flutter): cập nhật màn đặt cọc, hợp đồng, trả phòng theo API mới — hoặc chốt rõ mobile **đóng băng** ở mức nào cho buổi present | Nhóm quyết định | 3.1, 3.6 | M | |

### WS4 — Người ở cùng & điện nước
| ID | Việc | Owner | Phụ thuộc | Size | Người |
|----|------|-------|-----------|------|-------|
| 4.1 | Bảng `ContractOccupants` + API thêm/ghi ngày ra | Claude | 3.3 | M | |
| 4.2 | Chuyển người đứng tên (D4) | Claude | 4.1, 3.6 | L | |
| 4.3 | Hóa đơn dịch vụ theo đầu người, tính theo tỷ lệ ngày | Claude | 4.1 | M | |
| 4.4 | Trần giá điện/nước + cảnh báo chỉ số bất thường | Claude | 1.2 | S | |
| 4.5 | FE: quản lý người ở cùng, chuyển người đứng tên | AG | 4.1, 4.2 | M | |
| 4.6 | Test các tình huống bảng D4 | Codex | 4.2, 4.3 | M | |

### WS5 — Audit log
| ID | Việc | Owner | Phụ thuộc | Size | Người |
|----|------|-------|-----------|------|-------|
| 5.1 | Bảng `AuditLogs` + action filter (lớp 1) | Claude | — | M | |
| 5.2 | Ghi chủ động có trước/sau cho nghiệp vụ quan trọng (lớp 2) | Claude | 5.1, WS3 | M | |
| 5.3 | FE: trang Admin xem log có bộ lọc | AG | 5.1 | M | |
| 5.4 | Test: không lộ dữ liệu nhạy cảm, đủ log cho thao tác ghi | Codex | 5.1 | S | |

### WS6 — UI/UX & hiển thị
| ID | Việc | Owner | Phụ thuộc | Size | Người |
|----|------|-------|-----------|------|-------|
| 6.1 | Sidebar nhóm theo nghiệp vụ (xem bên dưới) | AG | — | S | |
| 6.2 | **Một** component chi tiết phòng và **một** cho khu trọ, dùng chung cho mọi vai trò (vai trò chỉ thêm nút thao tác), có danh sách thiết bị | AG | 2.3 | M | |
| 6.3 | Dashboard layout lại cho liền mạch: page template, header, empty/loading state thống nhất | AG | 6.1 | L | |
| 6.4 | Xóa file Storage khi xóa phòng/khu trọ | Claude | — | S | |
| 6.5 | Review UI: accessibility, responsive, không vỡ luồng cũ | Codex | 6.3 | S | |

Sidebar đề xuất:
```
Tổng quan        Dashboard
Khu trọ & Phòng  Khu trọ · Phòng · Loại phòng · Thiết bị
Cho thuê         Lịch xem phòng · Đặt cọc · Hợp đồng
Tài chính        Hóa đơn · Đối soát thanh toán · Hoàn cọc
Vận hành         Bảo trì / Sửa chữa
Quản trị         Người dùng · Phân công Manager · Báo cáo · Nhật ký hoạt động
```

### WS7 — Tài liệu & demo
| ID | Việc | Owner | Phụ thuộc | Size | Người |
|----|------|-------|-----------|------|-------|
| 7.1 | Cập nhật SRS (`docs/SRS_HeThongThuePhongTro_v1.1.md`) theo các quyết định ở mục 2 | Nhóm | mục 2 chốt | M | |
| 7.2 | Kịch bản demo "trước và sau" cho buổi present | Nhóm | WS3 | S | |
| 7.3 | Dữ liệu demo sạch (khu trọ thật, địa chỉ 2 cấp, ảnh thật) | Nhóm | 1.4 | S | |

---

## 4. Thứ tự thực hiện

```
Giai đoạn 0  Chốt các mục 🔒 ở phần 2 (cả nhóm, 1 buổi họp) + hỏi Châu về migration (câu 8)
Giai đoạn 1  WS1 + WS2           ← nền tảng, mọi thứ khác dựa vào
             (việc 1.1 không cần migration nên bắt đầu được ngay)
             song song: 6.1 (sidebar), 5.1 (audit lớp 1)
Giai đoạn 2  WS3                 ← luồng chính, ưu tiên cao nhất
             song song: 6.2, 6.3 (UI)
Giai đoạn 3  WS4 + 5.2
Giai đoạn 4  Test end-to-end, WS7, tổng duyệt demo
```
Nếu thiếu thời gian, cắt theo thứ tự: 4.4 → 4.2 → 5.3 → 3.5 (làm bản đơn giản). **Không cắt** 3.3 và 3.6, vì đây là lỗi nghiệp vụ lớn nhất.

---

## 5. Phối hợp giữa 3 AI và nhóm

Ba AI **không nói chuyện trực tiếp** với nhau. Mọi trao đổi đi qua file trong repo, thành viên nhóm chuyển lời.

| Vai | Được sửa | Không được sửa |
|-----|----------|----------------|
| Claude | `PRN232_BE/**` (trừ project test), `docs/rebuild/API-CHANGES.md` | `PRN232_FE/**` |
| Antigravity | `PRN232_FE/src/**` | `PRN232_BE/**`, `PRN232_Mobile/**` |
| Codex | `PRN232_BE/PRN232.Application.Tests/**`, `PRN232_FE/tests/**`, `docs/rebuild/reviews/**` | Code chính (chỉ đề xuất trong review) |

Quy trình cho mỗi task:
1. Mỗi task làm trên branch riêng `rebuild/<id>-<tên-ngắn>` (ví dụ `rebuild/3.3-deposit-to-contract`), tách từ `main`.
2. Task có đổi API: Claude ghi endpoint và DTO mới vào `docs/rebuild/API-CHANGES.md` **trước** khi code. Antigravity đọc file này để code FE song song, dùng mock trong lúc chờ BE.
3. Xong task: Codex review diff và viết test theo tiêu chí ở mục 2, ghi nhận xét vào `docs/rebuild/reviews/<id>.md`.
4. Thành viên phụ trách đọc review, chạy thử, rồi mở PR vào `main`.

**Quy tắc migration:** tại một thời điểm chỉ **một branch** được tạo EF migration. Migration được merge theo thứ tự để không vỡ `AppDbContextModelSnapshot`. DB Supabase dùng chung, nên chỉ chạy migration sau khi PR đã merge.

**Điều kiện đặc biệt từ main:** migration `20261114085610_PropertyScopedRoomTypesAndViewingFlow` của Châu **chưa chạy trên DB Live** và đang chờ backup + rehearsal PostgreSQL 17. Hệ quả cho rebuild:
- Mọi migration của rebuild phải sinh **sau** migration này (tạo từ nhánh đã có nó, không từ nhánh cũ hơn).
- Không chạy migration nào của rebuild lên DB Live trước khi migration của Châu được áp dụng.
- Phần lớn việc BE ở WS2–WS5 cần migration, nên **hỏi Châu lịch áp dụng** ngay từ đầu để tránh nghẽn.
- Máy local chưa có DB riêng; các test PostgreSQL đang bị bỏ qua (24 test skipped). Nên dựng một PostgreSQL local (Docker) để chạy migration trước khi đụng Supabase dùng chung.

**Mobile:** `PRN232_Mobile` gọi cùng API. Mỗi task đổi hợp đồng API phải ghi vào `API-CHANGES.md` kèm mục "ảnh hưởng mobile". Quyết định ở việc 3.9: cập nhật mobile hay đóng băng.

---

## 6. Câu hỏi cần chốt (Giai đoạn 0)

1. Mức cọc: cố định 1 tháng, hay chủ trọ cấu hình 1–2 tháng?
2. Chủ trọ hủy: chỉ hoàn cọc, hay hoàn cọc **và** phạt thêm một khoản bằng cọc theo Điều 328 BLDS?
3. Thời hạn báo trước khi trả phòng sớm: 30 ngày?
4. Thời hạn hợp đồng tối thiểu: 1, 3 hay 6 tháng?
5. Các con số trong bảng D2 (diện tích, giá, số người) — nhóm đồng ý hay chỉnh?
6. "Hóa đơn vs thiết bị" — hỏi lại giảng viên xem có đúng cách hiểu ở D6 không.
7. Phân công thành viên vào cột **Người** ở mục 3.
8. **Hỏi Châu:** (a) lịch chạy migration `PropertyScopedRoomTypesAndViewingFlow` lên DB Live; (b) khu trọ cũ còn dùng `District` không, có thể migrate hết sang mã tỉnh/phường rồi bỏ cột; (c) `/api/locations` có mở public cho khách không.
9. **Mobile:** cập nhật theo luồng cọc mới hay đóng băng ở mức nào cho buổi present?
10. **Giá cũ bất thường:** RoomType 1 giá 3.000đ và Booking 4 (cọc 1.000đ so với hợp đồng 2.000.000đ) đang chờ người đối soát. Ai xử lý trước khi demo?

---

## Nguồn tham khảo
- Tài liệu rollout của Châu trên main: `PRN232_PROPERTY_SCOPED_ROOM_TYPES_ROLLOUT.md`
- Sắp xếp đơn vị hành chính 2025: [Thư viện Pháp luật – danh sách 3.321 xã/phường](https://thuvienphapluat.vn/hoi-dap-phap-luat/danh-sach-3321-dvhc-cap-xa-cua-34-tinh-thanh-sau-sap-nhap-2025-chi-tiet-ra-sao-138053823.html), [VCCI – kết thúc cấp huyện từ 1/7/2025](https://www.vcci.com.vn/tin-tuc/ket-thuc-hoat-dong-cua-don-vi-hanh-chinh-cap-huyen-tu-ngay-1-7-2025)
- Giá điện cho người thuê trọ: [VOH – áp giá bậc 3](https://voh.com.vn/tin-tp-ho-chi-minh/tphcm-ap-dung-gia-ban-le-dien-sinh-hoat-bac-3-cho-nguoi-thue-tro-569652.html), [EVNSPC – phạt thu giá điện cao](https://cskh.evnspc.vn/NewLetter/Detail?newId=889)
- Diện tích phòng trọ: [VnExpress – đề xuất 5 m²/người](https://vnexpress.net/gia-nha-tro-luong-cong-nhan-4778495.html), [Batdongsan wiki – chưa có chuẩn toàn quốc](https://wiki.batdongsan.com.vn/wiki/dien-tich-phong-tro-cdk-784698)
- Thực tế tiền cọc: [Muaban – tiền cọc thuê trọ](https://muaban.net/blog/tien-coc-thue-phong-tro-la-gi-194825/), [Tranh chấp cọc khi một người chuyển trọ](https://xn--luts-tib7365b.net/bai-viet/giai-quyet-tranh-chap-tien-coc-khi-mot-nguoi-don-phuong-chuyen-tro)
- Đặt cọc: Điều 328 Bộ luật Dân sự 2015
