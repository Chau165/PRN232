# Kịch bản kiểm thử hệ thống TrọViệt

Phiên bản 1.2 • 14/11/2026 • Đối chiếu với SRS v1.2 (docs/SRS_HeThongThuePhongTro_v1.1.md) và mã nguồn nhánh main đã gộp các thay đổi đến ngày 14/11/2026 (hoàn cọc, chỉ số bàn giao, báo cáo tiền cọc, giữ phòng 7 ngày sau khi đặt cọc)

> Tên nút và thông báo ghi trong kịch bản lấy từ mã nguồn; nơi nào chữ trên giao diện thật khác thì ưu tiên chữ trên giao diện và sửa lại kịch bản.

Tài liệu này là kịch bản **kiểm thử thủ công từng bước trên bản đã triển khai** (Railway). Mỗi bước ghi tài khoản, thao tác, kết quả mong đợi và mã yêu cầu SRS tương ứng. Kiểm thử tự động (xUnit, Vitest) nằm ở mục 14.

> Kịch bản không chứa mật khẩu hay khóa nào. Tài khoản thử do nhóm cung cấp riêng, không ghi vào repo.

## 0. Quy ước

| Ký hiệu | Ý nghĩa |
| --- | --- |
| ✅ PASS | Kết quả đúng như mong đợi |
| ❌ FAIL | Sai; ghi lại mã bước, ảnh chụp, giờ xảy ra |
| ⏭ BỎ QUA | Chưa thể thử (thiếu điều kiện); ghi lý do |
| 💰 | Bước dùng **tiền thật** qua payOS; chỉ làm với phòng/hóa đơn giá thấp |
| 🔎 | Bước kiểm tra phía kỹ thuật (Swagger, DevTools, log), không phải thao tác người dùng |

Địa chỉ:

| Thành phần | Địa chỉ |
| --- | --- |
| Frontend | https://prn232fe-production.up.railway.app |
| Backend, Swagger | https://prn232-production.up.railway.app/swagger |
| Sức khỏe backend | /api/health-checks và /api/health-checks/database |

## 1. Chuẩn bị (làm một lần, khoảng 10 phút)

### 1.1 Tài khoản và dữ liệu

| Mã | Cần có | Ghi chú |
| --- | --- | --- |
| A | 1 tài khoản **Admin** | Quản trị |
| M1 | 1 tài khoản **Manager** được phân công khu trọ KT1 | |
| M2 | 1 tài khoản **Manager** KHÔNG được phân công KT1 | Dùng để thử phân quyền |
| C1 | 1 tài khoản **Customer** | Khách chính |
| C2 | 1 tài khoản **Customer** thứ hai | Thử hai người cùng giữ một phòng |
| KT1 | 1 khu trọ có tọa độ hợp lệ | |
| P1 | 1 phòng Available trong KT1, **giá thuê thấp** | Tiền cọc bằng đúng giá thuê một tháng, hãy chọn phòng rẻ nhất |
| P2 | 1 phòng Available thứ hai trong KT1 | Dùng cho luồng hoàn cọc |
| LP | 1 loại phòng gắn dịch vụ **điện** và **nước** | Hóa đơn lấy dịch vụ theo loại phòng |

### 1.2 Điều kiện hạ tầng (🔎)

| # | Kiểm tra | Mong đợi |
| --- | --- | --- |
| 1 | Mở /api/health-checks/database | `{"database":"connected"}` |
| 2 | Railway > Variables | Có `PayOS__Enabled=true`, `PayOS__BookingPaymentsEnabled=true`, ReturnUrl và CancelUrl trỏ /payment-result |
| 3 | Dashboard payOS | Webhook trỏ `<backend>/api/payments/payos/webhook` |
| 4 | Railway > Deployments | Bản deploy mới nhất chứa các thay đổi cần thử (đã merge `Tan` vào `main`) |

Nếu mục 2 hoặc 3 thiếu, các bước 💰 sẽ không ghi nhận tiền: dừng và sửa cấu hình trước.

## 2. Luồng 0: kiểm tra nhanh (5 phút)

| Bước | Tài khoản | Thao tác | Kết quả mong đợi | SRS |
| --- | --- | --- | --- | --- |
| 0.1 | Không | Mở trang chủ | Trang tải, hiện bản đồ và danh sách khu trọ | FR11, FR12 |
| 0.2 | Không | Mở một khu trọ rồi một phòng | Có ảnh, tiện ích, dịch vụ, thiết bị | FR13 |
| 0.3 | C1 | Đăng nhập | Vào được, thanh điều hướng hiện đúng chức năng khách | FR02 |
| 0.4 | M1 | Đăng nhập | Vào khu quản lý, **không** thấy mục Người dùng, Xét duyệt hoàn cọc, Đối soát thanh toán | FR05 |
| 0.5 | A | Đăng nhập | Thấy đủ mục, gồm Xét duyệt hoàn cọc và Đối soát thanh toán | FR05 |
| 0.6 | Không | Mở thẳng `/management/dashboard` khi chưa đăng nhập | Bị chuyển về trang đăng nhập | FR05 |

Nếu bước 0.1 đến 0.3 lỗi thì dừng, các luồng sau không có ý nghĩa.

## 3. Luồng 1: cấu hình khu trọ (Admin)

| Bước | Tài khoản | Thao tác | Kết quả mong đợi | SRS |
| --- | --- | --- | --- | --- |
| 1.1 | A | Khu trọ > Chi tiết KT1 | Thấy mục **Bảng giá dịch vụ** | FR10 |
| 1.2 | A | Thêm giá Điện: cách tính "Theo số điện (kWh)", đơn giá 3.500, từ tháng hiện tại, để trống tháng kết thúc | Thông báo thêm giá thành công; dòng mới xuất hiện kèm nhãn **Đang áp dụng** | FR10 |
| 1.3 | A | Thêm giá Nước: "Theo số nước (m³)", 18.000, cùng tháng | Thành công | FR10 |
| 1.4 | A | Thêm lại giá Điện cùng kỳ | Báo kỳ áp dụng bị trùng; giá cũ không đổi | FR10, BR09 |
| 1.5 | A | Xem danh sách "Cách tính tiền" | Chỉ có 3 lựa chọn: kWh, m³, cố định theo phòng | BR10 |
| 1.6 | M1 | Mở chi tiết KT1 | Thấy bảng giá nhưng **không có** form thêm mức giá | FR10 |
| 1.7 | A | Quản lý Manager > phân công M1 cho KT1 (nếu chưa) | M1 xuất hiện trong danh sách Manager của KT1 | FR07 |

> Dịch vụ thiếu giá trong tháng sẽ làm hóa đơn không phát hành được (thử ở bước 6.9).

## 4. Luồng 2: lịch xem phòng

| Bước | Tài khoản | Thao tác | Kết quả mong đợi | SRS |
| --- | --- | --- | --- | --- |
| 2.1 | C1 | Chọn phòng P1 > đặt lịch xem, chọn thời gian | Gửi thành công; Lịch xem và đặt cọc hiện trạng thái chờ duyệt | FR16 |
| 2.2 | C1 | Gửi lịch xem lần hai cho cùng phòng khi lần một còn chờ | Bị từ chối (đã có yêu cầu đang mở) | FR16 |
| 2.3 | A | Quản lý > Lịch xem > duyệt, gán M1 | C1 nhận thông báo; trang C1 hiện tên Manager phụ trách | FR17 |
| 2.4 | A | Với một lịch xem khác: Từ chối kèm lý do | C1 thấy lý do từ chối | FR17 |

## 5. Luồng 3: giữ chỗ và đặt cọc qua payOS 💰

Tiền cọc bằng giá thuê một tháng của P1.

| Bước | Tài khoản | Thao tác | Kết quả mong đợi | SRS |
| --- | --- | --- | --- | --- |
| 3.0 | C1 | Chi tiết phòng P1 > đặt cọc, **chưa** bấm giữ phòng | Trang báo trước: tiền cọc được trừ vào tiền phòng tháng đầu; phòng được giữ 7 ngày sau khi đặt cọc, quá hạn thì có thể mất cọc | FR56, BR14 |
| 3.1 | C1 | Chi tiết phòng P1 > đặt cọc > **Giữ phòng và thanh toán cọc** | Chuyển sang bước thanh toán; thấy "Giữ phòng đến" cách hiện tại 15 phút | FR18, FR19 |
| 3.2 | C2 | Cùng lúc thử giữ phòng P1 | Báo "Phòng vừa được người khác giữ hoặc không còn trống." | FR18, BR01 |
| 3.3 | C1 | Bấm **Thanh toán … qua payOS** | Chuyển sang trang pay.payos.vn; số tiền đúng tiền cọc | FR20 |
| 3.4 | C1 | 🔎 Ở trang kết quả, sửa tay URL thêm `?status=PAID&code=00` rồi tải lại | Trang **không** báo thành công khi tiền chưa được xác nhận | FR20, BR03 |
| 3.5 | C1 | Quay lại từ checkout mà **không** trả tiền (nút hủy) | Lúc đầu trang kết quả báo đang chờ xác nhận; trong khoảng 1 phút (worker đối soát hỏi lại payOS) tự chuyển sang "Liên kết cọc đã đóng"; booking vẫn Chờ thanh toán và vẫn thanh toán lại được | FR21, BR03 |
| 3.6 | C1 | Quay lại Lịch xem và đặt cọc > Thanh toán cọc > trả tiền thật 💰 | Trang kết quả hiện **Đặt cọc thành công**, "Booking #… " và nút Về booking của tôi | FR20, FR21 |
| 3.7 | C1 | Mở Lịch xem và đặt cọc | Booking ở trạng thái **Đã đặt cọc**, dòng "Phòng được giữ đến <ngày giờ> (7 ngày kể từ khi đặt cọc)" kèm cảnh báo quá hạn thì tiền cọc không được hoàn | BR02, FR56 |
| 3.8 | M1 | Quản lý > Đặt phòng và đặt cọc | Thấy booking của C1 với trạng thái Đã đặt cọc; cột Giữ phòng đến hiện đúng hạn 7 ngày; **không có** nút Giải phóng phòng vì chưa quá hạn | FR28, FR56 |
| 3.9 | M1 hoặc A | Quản lý > Báo cáo, chọn tháng hiện tại, xem mục **Tiền cọc thu / hoàn** | Có khoản cọc vừa trả trong cột Thu cọc của khu trọ KT1; tách riêng với doanh thu hóa đơn | FR51 |
| 3.10 | C1 | Mở lại URL trang kết quả sau khi đã thành công (tải lại) | Vẫn báo thành công, **không** thanh toán thêm | FR21 |
| 3.11 | Không | Để một booking Pending quá 15 phút | Booking hiện "Hết hạn thanh toán" và phòng được nhả | FR19 |
| 3.12 | 🔎 | Swagger: gọi `POST /api/bookings/{id}/pay` bằng token Customer | **409** "Use payOS checkout" (API giả lập bị khóa) | NFR05 |
| 3.13 | 🔎 M1 | Swagger: gọi `POST /api/bookings/{id}/cancel` cho booking vừa đặt cọc ở bước 3.6 | **409**, booking vẫn Đã đặt cọc (chưa hết 7 ngày giữ phòng) | BR04, BR13 |

### 5.1 Giải phóng phòng quá hạn giữ cọc

Cần một booking đã cọc quá 7 ngày, chưa có hợp đồng và không có yêu cầu hoàn cọc đang xử lý. Trên cơ sở dữ liệu thử có thể lùi `PaidAtUtc` của khoản cọc trong bảng `Payments` về 8 ngày trước thay vì chờ.

| Bước | Tài khoản | Thao tác | Kết quả mong đợi | SRS |
| --- | --- | --- | --- | --- |
| 3.14 | M1 | Quản lý > Đặt phòng và đặt cọc | Booking quá hạn có nhãn đỏ **Quá hạn nhận phòng** và nút **Giải phóng phòng**; booking còn hạn không có nút này | FR56 |
| 3.15 | M2 | Gọi `POST /api/bookings/{id}/cancel` cho booking quá hạn của KT1 | **403**, booking không đổi | BR06 |
| 3.16 | M1 | Bấm **Giải phóng phòng**, đọc hộp xác nhận rồi bấm Hủy | Hộp xác nhận nói rõ phòng sẽ mở cho người khác thuê và tiền cọc không được hoàn; bấm Hủy thì không có gì thay đổi | FR56 |
| 3.17 | M1 | Bấm lại **Giải phóng phòng** và xác nhận | Thông báo "Đã giải phóng phòng … và thông báo cho khách"; booking chuyển **Đã huỷ**; phòng hiện lại trong danh sách phòng trống công khai | FR56, BR13 |
| 3.18 | C1 | Chuông thông báo | Có thông báo "…đã được giải phóng do quá hạn nhận phòng", nội dung ghi tiền cọc không được hoàn | FR50, FR56 |
| 3.19 | A | Báo cáo > Tiền cọc thu / hoàn | Khoản cọc của booking vừa giải phóng vẫn nằm ở cột Thu cọc, không có dòng hoàn | FR51, BR13 |
| 3.20 | M1 | Với một booking quá hạn nhưng khách đã gửi yêu cầu hoàn cọc (đang chờ duyệt) | Không có nút Giải phóng phòng; gọi API hủy trả **409** | BR13 |

## 6. Luồng 4: hoàn cọc

Dùng booking đã cọc payOS của phòng P2 (làm lại bước 3.1 đến 3.6 cho P2 với C1). Booking cọc bằng thanh toán giả lập **không** đủ điều kiện hoàn cọc.

| Bước | Tài khoản | Thao tác | Kết quả mong đợi | SRS |
| --- | --- | --- | --- | --- |
| 4.1 | C1 | Lịch xem và đặt cọc > booking P2 > **Xin hoàn cọc** | Mở khung **Yêu cầu hoàn cọc**; hiện số tiền cọc để đối chiếu, **không** có ô nhập số tiền | FR23 |
| 4.2 | C1 | Bấm **Gửi yêu cầu hoàn cọc** khi chưa nhập đủ lý do, ngân hàng, số tài khoản | Không gửi được, có thông báo yêu cầu kiểm tra lại thông tin | FR23 |
| 4.3 | C1 | Nhập lý do, ngân hàng, số tài khoản có số 0 đầu (ví dụ 0123456789), tên chủ tài khoản; gửi | Thành công; lịch sử hiện **Chờ Admin xét duyệt**; số tài khoản hiển thị dạng che (`****6789`); không gửi thêm được yêu cầu thứ hai | FR23, FR24, BR12 |
| 4.4 | M1 | Quản lý > Đặt phòng và đặt cọc | Booking có nhãn **Chờ Admin xét duyệt**; **không có** nút Tạo hợp đồng; nút Huỷ booking không có (chỉ booking chưa thanh toán mới huỷ được) | FR22, FR26, FR28 |
| 4.5 | M1 | Bấm **Theo dõi hoàn cọc #…** | Xem được trạng thái và lịch sử hoàn cọc, **không có** form gửi yêu cầu | FR28 |
| 4.6 | A | Mở menu **Xét duyệt hoàn cọc** (hoặc nút cùng tên trong trang Đặt phòng và đặt cọc) | Hàng chờ có yêu cầu của C1; có bộ lọc trạng thái, gồm "Tất cả trạng thái" | FR25 |
| 4.7 | A | Mở yêu cầu | Thấy cọc thực nhận, số tiền yêu cầu, lý do, thông tin tài khoản để đối chiếu | FR25, BR12 |
| 4.8 | A | Thử duyệt với số tiền lớn hơn số tiền yêu cầu, hoặc không có ghi chú | Bị chặn, có thông báo lỗi | FR25 |
| 4.9 | A | Từ chối kèm lý do (làm trên yêu cầu thử) | Trạng thái **Đã từ chối** | FR25 |
| 4.10 | C1 | Mở lại booking | Thấy lý do từ chối; có thể **gửi lại** yêu cầu | FR24 |
| 4.11 | M1 | Booking sau khi bị từ chối | Nút **Tạo hợp đồng** hiện lại | FR26 |
| 4.12 | A | Duyệt đúng quy trình (số tiền hợp lệ, có ghi chú) | Trạng thái **Đã duyệt, chờ chuyển tiền** | FR25, BR11 |
| 4.13 | 🔎 | Kiểm tra sau bước 4.12 | Nếu kênh chi **tắt**: trạng thái dừng ở Đã duyệt (đúng thiết kế). Nếu đã bật: sang Đang xử lý rồi **Đã hoàn cọc** và booking thành Đã huỷ | FR27 |

Bước 4.13 chỉ làm hết khi có khóa kênh chi riêng; nếu chưa có, ghi ⏭ BỎ QUA.

## 7. Luồng 5: lập và quản lý hợp đồng

Dùng booking đã cọc của phòng P1 (C1). Booking không có yêu cầu hoàn đang xử lý.

| Bước | Tài khoản | Thao tác | Kết quả mong đợi | SRS |
| --- | --- | --- | --- | --- |
| 5.1 | M1 | Đặt phòng và đặt cọc > **Tạo hợp đồng** | Mở form, thông tin khách và phòng đã điền sẵn | FR29 |
| 5.2 | M1 | Nhập ngày bắt đầu trong quá khứ | Bị từ chối | FR29 |
| 5.3 | M1 | Nhập ngày kết thúc trước ngày bắt đầu | Bị từ chối | FR29 |
| 5.4 | M1 | Nhập ngày bắt đầu là hôm nay, kết thúc sau vài tháng; lưu | Hợp đồng tạo thành công; C1 nhận thông báo | FR29, FR30 |
| 5.5 | M1 | Quản lý > Phòng | Phòng P1 chuyển **Occupied** | FR30 |
| 5.6 | M1 | Tạo hợp đồng lần hai từ cùng booking | Bị từ chối (booking đã có hợp đồng) | BR05 |
| 5.7 | M2 | Mở hợp đồng vừa tạo (thử vào trực tiếp) | Không thấy hợp đồng; thao tác bị từ chối | BR06 |
| 5.8 | C1 | Hợp đồng của tôi; Phòng của tôi | Thấy hợp đồng và phòng đang thuê; xem được dòng thời gian hóa đơn | FR33 |
| 5.9 | M1 | **Gia hạn**: ngày mới sau ngày kết thúc hiện tại | Thành công; C1 nhận thông báo | FR31 |
| 5.10 | M1 | Gia hạn với ngày sớm hơn ngày kết thúc hiện tại | Bị từ chối | FR31 |

## 8. Luồng 6: chỉ số điện nước và hóa đơn

| Bước | Tài khoản | Thao tác | Kết quả mong đợi | SRS |
| --- | --- | --- | --- | --- |
| 6.0 | M1 | Hóa đơn > **Tạo hóa đơn**, chọn hợp đồng **chưa** có chỉ số bàn giao | Hệ thống không cho nhập chỉ số kỳ đầu; mở form **Ghi nhận chỉ số bàn giao** | FR55, FR36 |
| 6.1 | M1 | Quản lý > Hợp đồng > hợp đồng của C1 > **Ghi nhận chỉ số bàn giao**: ngày bàn giao là hôm nay, điện 100, nước 10 | Thông báo "Đã lưu chỉ số bàn giao thành công."; nút đổi thành **Sửa chỉ số bàn giao** | FR55 |
| 6.1a | M1 | Nhập ngày bàn giao ở tương lai, hoặc trước ngày bắt đầu hợp đồng | Bị từ chối | FR55 |
| 6.1b | C1 | Hợp đồng của tôi > chi tiết | Thấy chỉ số bàn giao ở chế độ chỉ đọc | FR55 |
| 6.2 | M1 | Hóa đơn > **Tạo hóa đơn**, chọn hợp đồng và **tháng bắt đầu hợp đồng**; ô chỉ số đầu kỳ đã điền sẵn (điện 100, nước 10) và không sửa được; nhập chỉ số cuối điện 160, nước 15 | Lưu thành công | FR35 |
| 6.3 | M1 | Nhập chỉ số cuối nhỏ hơn chỉ số đầu kỳ | Bị từ chối | FR36 |
| 6.4 | M1 | Ghi chỉ số cho tháng tương lai | Bị từ chối | FR36 |
| 6.4a | M1 | Ở hợp đồng khác đã có kỳ đầu: bỏ qua một tháng rồi ghi tháng sau nữa | Bị từ chối, yêu cầu chốt các kỳ liên tiếp | FR36 |
| 6.4b | M1 | Sau khi đã có chỉ số hoặc hóa đơn, thử sửa chỉ số bàn giao | Không còn sửa được | FR55 |
| 6.5 | M1 | **Xem trước** hóa đơn | Có dòng tiền phòng, điện (60 x 3.500 = 210.000), nước (5 x 18.000 = 90.000) | FR37, FR38 |
| 6.6 | M1 | Kiểm tra dòng khấu trừ | Tháng đầu có dòng "Tiền cọc đã thanh toán - khấu trừ tiền phòng"; tiền phòng ròng bằng 0 nếu cọc bằng tiền phòng | FR39, BR07 |
| 6.7 | M1 | Hạn thanh toán là ngày trong quá khứ | Bị từ chối | FR37 |
| 6.8 | M1 | Phát hành hóa đơn | Thành công; C1 nhận thông báo; hóa đơn Unpaid | FR37 |
| 6.9 | M1 | Phát hành hóa đơn thứ hai cho cùng tháng | Bị từ chối (đã có hóa đơn tháng này) | FR39 |
| 6.10 | A, M1 | **Tùy chọn:** gắn thêm một dịch vụ chưa có giá vào loại phòng LP, rồi lập hóa đơn cho một tháng chưa có hóa đơn | Hóa đơn không phát hành được, báo dịch vụ chưa có giá | FR38 |

BR08 (đổi giá không làm đổi hóa đơn cũ) được kiểm chứng bằng kiểm thử tự động, không thử tay: giá "không thời hạn" tạo ở bước 1.2 không đổi được bằng giá mới vì kỳ chồng lấn (giới hạn L04).

## 9. Luồng 7: thanh toán hóa đơn

| Bước | Tài khoản | Thao tác | Kết quả mong đợi | SRS |
| --- | --- | --- | --- | --- |
| 7.1 | C1 | Hóa đơn của tôi > mở hóa đơn > **Thanh toán qua payOS** 💰 | Chuyển checkout; số tiền đúng | FR40 |
| 7.2 | C1 | Trả tiền, quay lại | Trang kết quả báo **Thanh toán thành công**, "Hóa đơn #…" và nút Về hóa đơn của tôi | FR40 |
| 7.3 | M1 | Hóa đơn | Hóa đơn chuyển **Paid**; thông báo cho Manager | FR40 |
| 7.4 | M1 | Báo cáo | Doanh thu tăng đúng tổng hóa đơn, ghi theo thời điểm thu tiền | FR51 |
| 7.5 | M1 | Với một hóa đơn khác chưa trả: **ghi nhận tiền mặt** | Hóa đơn Paid, phương thức Tiền mặt | FR40 |
| 7.6 | M1 | Bấm ghi nhận tiền mặt lần hai cho cùng hóa đơn | Bị từ chối (đã trả) | FR40 |
| 7.7 | A | Đối soát thanh toán | Mở được trang; hàng chờ trống nếu chưa có trả trùng | FR41 |
| 7.8 | A | Nếu có mục: chọn "Đã hoàn tiền ngoài hệ thống" mà không nhập mã chứng từ | Bị chặn | FR41 |

Không cố ý trả trùng bằng tiền thật. Trường hợp trả trùng đã được kiểm thử tự động (mục 14).

## 10. Luồng 8: khiếu nại hóa đơn

| Bước | Tài khoản | Thao tác | Kết quả mong đợi | SRS |
| --- | --- | --- | --- | --- |
| 8.1 | C1 | Hóa đơn của tôi > khiếu nại, chọn một dòng phí, nhập nội dung | Gửi thành công | FR45 |
| 8.2 | M1 | Hóa đơn > tab khiếu nại, đổi trạng thái sang InProgress rồi Resolved | C1 nhận thông báo mỗi lần đổi | FR46 |

## 11. Luồng 9: bảo trì thiết bị

| Bước | Tài khoản | Thao tác | Kết quả mong đợi | SRS |
| --- | --- | --- | --- | --- |
| 9.1 | M1 hoặc A | Thiết bị > thêm một thiết bị cho phòng P1 | Thiết bị xuất hiện | FR08 |
| 9.2 | C1 | Phòng của tôi > **báo hỏng** thiết bị, nhập mô tả | Yêu cầu tạo thành công, trạng thái chờ | FR47 |
| 9.3 | A | Bảo trì > giao cho M1 | Trạng thái chuyển Approved | FR48 |
| 9.4 | M1 | Nộp báo cáo hoàn thành kèm 1 đến 5 ảnh | Trạng thái Completed; có lịch sử đổi trạng thái | FR49 |
| 9.5 | M1 | Nộp báo cáo mà không có ảnh | Bị từ chối | FR49 |
| 9.6 | C1 | Mở yêu cầu | Thấy lịch sử và thông báo | FR49, FR50 |

## 12. Luồng 10: kết thúc hợp đồng

| Bước | Tài khoản | Thao tác | Kết quả mong đợi | SRS |
| --- | --- | --- | --- | --- |
| 10.1 | M1 | Hợp đồng còn hóa đơn chưa trả > **Kết thúc**, tick đã bàn giao | Hệ thống yêu cầu xác nhận đã biết số nợ | FR32 |
| 10.2 | M1 | Xác nhận, chọn phòng chưa sẵn sàng (chuyển Maintenance) | Hợp đồng Terminated; phòng ở Maintenance; C1 nhận thông báo | FR32 |
| 10.3 | M1 | Đánh dấu phòng sẵn sàng cho thuê | Phòng về Available | FR32 |
| 10.4 | M1 | Thử tạo hóa đơn hoặc ghi chỉ số cho hợp đồng đã kết thúc | Bị từ chối (chỉ hợp đồng đang hiệu lực) | L01 |
| 10.5 | C1 | Thanh toán hóa đơn cũ còn nợ sau khi hợp đồng kết thúc | Vẫn thanh toán được | FR40 |

Bước 10.4 chứng minh giới hạn L01 trong SRS; đây là kết quả đúng của hệ thống hiện tại, không phải lỗi.

## 13. Luồng 11: phân quyền và thông báo

| Bước | Tài khoản | Thao tác | Kết quả mong đợi | SRS |
| --- | --- | --- | --- | --- |
| 11.1 | C1 | Gõ thẳng `/management/dashboard` | Bị chặn hoặc chuyển trang | FR05 |
| 11.2 | M1 | Gõ thẳng `/management/refunds`, `/management/payment-reviews`, `/management/users` | Bị chặn | FR05 |
| 11.3 | M2 | Xem danh sách booking, hợp đồng, hóa đơn | **Không** thấy dữ liệu của KT1 | BR06 |
| 11.4 | A | Thu hồi phân công M1 khỏi KT1 | Ngay sau đó M1 không còn xem hoặc sửa được dữ liệu KT1 | BR06 |
| 11.5 | 🔎 | Gọi API không token | **401** | NFR01 |
| 11.6 | 🔎 | Đăng nhập sai mật khẩu 6 lần trong 1 phút | Lần thứ 6 bị giới hạn tần suất (429) | NFR02 |
| 11.7 | C1 | Chuông thông báo | Thấy thông báo của các bước trên; có thể đánh dấu đã đọc; thông báo mới hiện không cần tải lại trang | FR50 |
| 11.8 | M1, A | Tổng quan và Báo cáo | Số liệu khớp với các bước đã làm; Manager chỉ thấy khu của mình | FR51, FR52 |

## 14. Kiểm thử tự động (chạy trước khi demo)

Chạy trong PowerShell hoặc Git Bash:

```bash
cd PRN232_FE
npx tsc --noEmit
npx vitest run --maxWorkers=1
npx vite build
cd ../PRN232_BE
dotnet test
```

| Bộ kiểm tra | Kết quả mong đợi (14/11/2026) |
| --- | --- |
| tsc | Không lỗi |
| Vitest | 354 đạt (45 file) |
| vite build | Thành công (cảnh báo kích thước chunk có sẵn, không ảnh hưởng) |
| dotnet test | 1468 đạt, 23 bỏ qua, **1 lỗi** |

Lỗi duy nhất của `dotnet test` là `SupabaseReadOnlyInspectionTests`: bài kiểm tra này cần biến môi trường `ConnectionStrings__DefaultConnection` trỏ đến cơ sở dữ liệu thật. Máy không đặt biến này thì bài sẽ lỗi; đây không phải lỗi mã. Các bài cần PostgreSQL riêng sẽ được bỏ qua nếu chưa cung cấp.

Kiểm thử liên thông thanh toán payOS bằng máy chủ giả (không ra mạng): `npm run test:payos-e2e` trong PRN232_FE, mô tả ở PRN232_BE/docs/payos-end-to-end-testing.md.

## 15. Những điều KHÔNG nên báo là lỗi

Các hành vi dưới đây đúng theo thiết kế hiện tại và đã nêu trong SRS mục 10:

| Hiện tượng | Lý do |
| --- | --- |
| Hoàn cọc dừng ở "Đã duyệt, chờ chuyển tiền" | Kênh chi payOS đang tắt (FR27, L07) |
| Hợp đồng đã kết thúc không tạo được hóa đơn mới | L01 |
| Hóa đơn tháng đầu của tháng vào giữa tháng vẫn tính trọn tháng | L03 |
| Không đổi được giá dịch vụ đã tạo "không thời hạn" | L04 |
| Không có màn hình CCCD trên web | FR14, FR15 chưa làm phần web |
| Không có nhắc hạn tự động | FR44 chưa làm |
| Không nhập được phí theo người hoặc xe | FR43 chưa làm |
| Ứng dụng di động còn dữ liệu mẫu | FR53, FR54 |
| Duyệt lịch xem do Admin; Manager không tự duyệt | Thiết kế hiện tại (L06) |
| Manager không có nút huỷ booking đã đặt cọc trong 7 ngày đầu | Trong thời gian giữ phòng, booking đã cọc chỉ được giải phóng bằng hoàn cọc hoặc lập hợp đồng (BR04, BR13) |
| Booking quá hạn giữ phòng vẫn chiếm phòng | Giải phóng phòng làm bằng tay, hệ thống không tự hủy (L09) |
| Trả phòng không có bước hoàn cọc | Tiền cọc đã trừ vào tiền phòng tháng đầu (OD01, BR14); FR34 ngoài phạm vi |
| Hợp đồng kỳ đầu không nhập được chỉ số khi chưa ghi nhận chỉ số bàn giao | Quy tắc mới của FR55, không phải lỗi |

## 16. Thứ tự chạy khi chỉ có 30 phút trước buổi chấm

| Thứ tự | Luồng | Thời gian |
| --- | --- | --- |
| 1 | 1.2 mục hạ tầng, Luồng 0 | 5 phút |
| 2 | Luồng 3: bước 3.1 đến 3.7 (một lần cọc thật) | 8 phút |
| 3 | Luồng 4: bước 4.1 đến 4.7, 4.12 | 6 phút |
| 4 | Luồng 5 và 6: hợp đồng, chỉ số, hóa đơn | 8 phút |
| 5 | Luồng 11: phân quyền | 3 phút |

Hóa đơn thanh toán thật (Luồng 7) và bảo trì (Luồng 9) chỉ chạy nếu còn thời gian.

## 17. Mẫu ghi nhận kết quả

| Bước | Kết quả | Ghi chú, mã đối tượng (bookingId, invoiceId, paymentRequestId) | Người test, giờ |
| --- | --- | --- | --- |
| | | | |
