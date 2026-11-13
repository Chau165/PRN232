# Đặc tả yêu cầu phần mềm
## Hệ thống quản lý thuê phòng trọ TrọViệt

Software Requirements Specification • Phiên bản 1.1 • Ngày 13 tháng 11 năm 2026

Tài liệu đặc tả yêu cầu của hệ thống quản lý thuê phòng trọ dành cho khách thuê, người quản lý khu trọ và quản trị viên. Phiên bản 1.1 được đối chiếu trực tiếp với mã nguồn tại thời điểm phát hành: mỗi yêu cầu chức năng ghi rõ mức hiện thực để người đọc phân biệt phần đã chạy được với phần còn là đề xuất.

### Thông tin tài liệu

| Thuộc tính | Nội dung |
| --- | --- |
| Mã tài liệu | SRS RENTAL 01 |
| Phiên bản | 1.1 (thay thế 1.0 ngày 12/11/2026) |
| Môn học | PRN232, FPT University HCM |
| Đối tượng đọc | Giảng viên hướng dẫn, nhóm phát triển, người kiểm thử |
| Phạm vi sản phẩm | Ứng dụng web và ứng dụng di động quản lý nhiều khu trọ, nhiều phòng |
| Ngôn ngữ nghiệp vụ | Tiếng Việt |
| Đơn vị tiền tệ | Đồng Việt Nam (VND) |
| Múi giờ nghiệp vụ | Asia/Ho_Chi_Minh |

### Cách đọc mức hiện thực

| Nhãn | Ý nghĩa |
| --- | --- |
| Đã làm | Có xử lý ở backend, có giao diện web và có kiểm thử tự động |
| Một phần | Đã có backend hoặc một phần luồng; còn thiếu giao diện, tích hợp hoặc nhánh nghiệp vụ được nêu rõ |
| Chưa làm | Yêu cầu đề xuất, chưa có mã nguồn |

### Lịch sử phiên bản

| Phiên bản | Nội dung |
| --- | --- |
| 1.0 (12/11/2026) | Đặc tả vòng đời thuê và thanh toán; đề xuất yêu cầu trả phòng, quyết toán và các quyết định còn mở |
| 1.1 (13/11/2026) | Mở rộng cho toàn bộ hệ thống (3 phạm vi và ứng dụng di động); cập nhật đặt cọc qua payOS, hoàn cọc booking, đối soát thanh toán, bảng giá dịch vụ theo khu trọ; bổ sung giới hạn và rủi ro hiện tại |

<!-- PAGE -->
# 1 Giới thiệu

### 1.1 Mục đích

Tài liệu thống nhất cách hệ thống vận hành nghiệp vụ cho thuê phòng trọ, làm căn cứ để thiết kế, kiểm thử và nghiệm thu. Mỗi yêu cầu có mã định danh để truy vết sang API, màn hình và ca kiểm thử.

### 1.2 Phạm vi sản phẩm

Hệ thống giúp khách thuê tìm phòng trên bản đồ, đặt lịch xem, giữ chỗ và đặt cọc trực tuyến, theo dõi hợp đồng, hóa đơn và báo hỏng thiết bị. Người quản lý vận hành các khu trọ được giao: duyệt hồ sơ, lập hợp đồng, ghi chỉ số điện nước, lập hóa đơn, thu tiền mặt và xử lý bảo trì. Quản trị viên quản lý khu trọ, tài khoản, phân công quản lý, bảng giá dịch vụ, xét duyệt hoàn cọc, đối soát thanh toán và xem báo cáo doanh thu.

Ngoài phạm vi: kế toán tổng hợp, hóa đơn điện tử thuế, chữ ký số hợp đồng, chia tiền tự động cho từng chủ trọ, trả góp hóa đơn, chấm dứt hợp đồng trước hạn có phạt.

### 1.3 Thuật ngữ

| Thuật ngữ | Ý nghĩa |
| --- | --- |
| Khu trọ (Property) | Một địa điểm cho thuê gồm nhiều phòng, có thể có nhiều Manager được phân công |
| Loại phòng (RoomType) | Nhóm phòng cùng đặc điểm; quyết định danh sách dịch vụ được tính trên hóa đơn |
| Booking | Lần giữ chỗ một phòng của khách thuê trước khi lập hợp đồng |
| Tiền cọc | Khoản khách trả để giữ phòng, bằng một tháng tiền thuê; được khấu trừ vào tiền phòng tháng đầu của hợp đồng |
| Yêu cầu thanh toán (PaymentRequest) | Một liên kết checkout payOS cho tiền cọc hoặc hóa đơn, có trạng thái riêng |
| Webhook | Thông báo payOS gửi về backend khi giao dịch hoàn tất; là căn cứ duy nhất để ghi nhận tiền online |
| Payout | Lệnh chi tiền từ tài khoản payOS của hệ thống sang tài khoản ngân hàng khách (dùng cho hoàn cọc) |
| Đối soát | Đối chiếu định kỳ trạng thái giao dịch với payOS để phát hiện thanh toán bị sót hoặc trả trùng |
| Bảng giá dịch vụ | Giá điện, nước, dịch vụ cố định theo từng khu trọ và kỳ áp dụng |

### 1.4 Tài liệu tham chiếu

- Bản SRS 1.0 ngày 12/11/2026 (docs/SRS_HeThongThuePhongTro.md).
- Tài liệu kỹ thuật trong PRN232_BE/docs: cấu hình payOS, webhook, đặt cọc payOS, hoàn cọc (P6 đến P12), kênh chi payOS.
- Mã nguồn các thư mục PRN232_BE, PRN232_FE, PRN232_Mobile tại commit phát hành phiên bản này.

<!-- PAGE -->
# 2 Mô tả tổng quan

### 2.1 Kiến trúc hệ thống

| Thành phần | Công nghệ và vai trò |
| --- | --- |
| Web frontend | React 19, Vite, TypeScript, Tailwind CSS; gọi backend qua REST, nhận thông báo realtime qua SignalR |
| Ứng dụng di động | Flutter (khách thuê và quản lý), gọi cùng REST API |
| Backend | ASP.NET Core 8 Web API theo kiến trúc nhiều tầng: Api, Application, Domain, Infrastructure |
| Cơ sở dữ liệu | PostgreSQL trên Supabase, quản lý lược đồ bằng EF Core Migrations |
| Lưu trữ tệp | Supabase Storage cho ảnh phòng, ảnh khu trọ, ảnh báo cáo bảo trì, giấy tờ |
| Thanh toán | payOS: kênh thu (tiền cọc, hóa đơn) và kênh chi (hoàn cọc) với cấu hình tách biệt |
| Tiến trình nền | Worker đối soát thanh toán, worker gửi payout, worker đối soát payout |
| Triển khai | Docker, Railway (backend và frontend), migration tự chạy trước mỗi lần deploy, CI trên GitHub |

### 2.2 Tác nhân và quyền

| Tác nhân | Mô tả và quyền chính |
| --- | --- |
| Khách vãng lai | Xem khu trọ, phòng trống, bản đồ và chi tiết phòng; đăng ký tài khoản |
| Customer (khách thuê) | Đặt lịch xem, giữ chỗ, đặt cọc, xin hoàn cọc, xem hợp đồng và phòng đang thuê, thanh toán và khiếu nại hóa đơn, báo hỏng thiết bị, tải giấy tờ tùy thân |
| Manager (quản lý) | Chỉ thao tác trên khu trọ đang được phân công: lập, gia hạn, kết thúc hợp đồng; ghi chỉ số; lập hóa đơn; ghi nhận tiền mặt; duyệt giấy tờ; xử lý bảo trì được giao; xem báo cáo trong phạm vi |
| Admin (quản trị) | Quản lý khu trọ, phòng, tài khoản và vai trò; phân công Manager; bảng giá dịch vụ; duyệt lịch xem và giao việc bảo trì; xét duyệt hoàn cọc; đối soát thanh toán; báo cáo toàn hệ thống |
| payOS | Hệ thống bên ngoài: tạo checkout, gửi webhook, thực hiện payout |

### 2.3 Ràng buộc và giả định

- Toàn bộ tiền thu trực tuyến đi vào một tài khoản payOS của Admin; kênh chi dùng khóa riêng.
- Quyền của Manager được xác định theo bảng phân công Manager với khu trọ tại thời điểm thao tác, không lưu cố định trên dữ liệu.
- Mỗi phòng chỉ có tối đa một hợp đồng đang hiệu lực; một khách có thể thuê nhiều phòng.
- Ngày nghiệp vụ (hạn hợp đồng, tháng hóa đơn, tháng chỉ số) tính theo lịch Việt Nam.
- Thanh toán giả lập chỉ dùng cho môi trường phát triển; khi bật payOS, các API thanh toán giả lập bị khóa.

<!-- PAGE -->
# 3 Vòng đời nghiệp vụ chính

### 3.1 Luồng thuê phòng từ đầu đến cuối

| Bước | Tác nhân | Mô tả |
| --- | --- | --- |
| 1 Tìm phòng | Khách | Tìm khu trọ theo khu vực, tiện ích và bản đồ; xem chi tiết phòng, giá dịch vụ, thiết bị |
| 2 Đặt lịch xem | Customer, Admin | Customer gửi lịch xem; Admin duyệt và giao Manager dẫn xem, hoặc từ chối kèm lý do |
| 3 Giữ chỗ | Customer | Tạo booking Pending; phòng được giữ 15 phút để thanh toán cọc |
| 4 Đặt cọc | Customer, payOS | Thanh toán cọc qua checkout payOS; webhook xác nhận thì booking chuyển Deposited và phòng được giữ cho đến khi lập hợp đồng hoặc hoàn cọc |
| 5a Hoàn cọc | Customer, Admin, payOS | Nếu không thuê: Customer xin hoàn, Admin duyệt, hệ thống chi tiền qua payOS, booking chuyển Cancelled |
| 5b Lập hợp đồng | Manager | Lập hợp đồng từ booking đã cọc; giá thuê và tiền cọc được chốt; phòng chuyển Occupied |
| 6 Hằng tháng | Manager | Ghi chỉ số điện nước, xem trước và phát hành hóa đơn; tiền cọc được khấu trừ vào tiền phòng tháng đầu |
| 7 Thanh toán | Customer, Manager | Customer trả qua payOS hoặc Manager ghi nhận tiền mặt; khách có thể khiếu nại từng khoản |
| 8 Trong thời gian thuê | Customer, Admin, Manager | Báo hỏng thiết bị, Admin giao Manager xử lý, Manager nộp báo cáo hoàn thành kèm ảnh |
| 9 Kết thúc | Manager | Gia hạn, hoặc xác nhận bàn giao và kết thúc hợp đồng; mở lại phòng khi đã sẵn sàng cho thuê |

<!-- PAGE -->
# 4 Yêu cầu chức năng

### 4.1 Tài khoản và phân quyền

| Mã | Yêu cầu và tiêu chí kiểm tra | Mức |
| --- | --- | --- |
| FR01 | Đăng ký tài khoản Customer với họ tên, email, số điện thoại, mật khẩu; kiểm tra định dạng dùng chung giữa backend và frontend | Đã làm |
| FR02 | Đăng nhập bằng email và mật khẩu, nhận JWT hiệu lực 120 phút; giới hạn 5 lần đăng nhập mỗi phút | Đã làm |
| FR03 | Người dùng xem và cập nhật hồ sơ, đổi mật khẩu | Đã làm |
| FR04 | Admin xem danh sách người dùng có phân trang, tạo tài khoản, đổi vai trò, khóa hoặc mở khóa | Đã làm |
| FR05 | Phân quyền theo vai trò Customer, Manager, Admin ở cả API và giao diện; truy cập ngoài quyền trả 403 | Đã làm |

### 4.2 Khu trọ, phòng và danh mục

| Mã | Yêu cầu và tiêu chí kiểm tra | Mức |
| --- | --- | --- |
| FR06 | Admin tạo, sửa, xóa khu trọ; địa chỉ được geocode thành tọa độ để hiển thị trên bản đồ | Đã làm |
| FR07 | Admin phân công và thu hồi Manager cho khu trọ; Manager xem danh sách khu được giao | Đã làm |
| FR08 | Admin quản lý phòng; Admin và Manager quản lý ảnh phòng (tải lên, đặt ảnh chính, xóa), tiện ích và thiết bị | Đã làm |
| FR09 | Quản lý loại phòng và danh sách dịch vụ gắn với loại phòng | Đã làm |
| FR10 | Admin cấu hình bảng giá dịch vụ theo khu trọ và kỳ áp dụng; kỳ không được chồng lấn; giá đã tạo không sửa hoặc xóa để giữ đúng hóa đơn cũ; Manager xem bảng giá | Đã làm |

### 4.3 Tìm phòng công khai

| Mã | Yêu cầu và tiêu chí kiểm tra | Mức |
| --- | --- | --- |
| FR11 | Danh sách khu trọ đang cho thuê có phân trang, lọc theo khu vực và tiện ích | Đã làm |
| FR12 | Hiển thị khu trọ trên bản đồ; khu trọ thiếu tọa độ được thông báo thay vì đặt sai vị trí | Đã làm |
| FR13 | Chi tiết khu trọ, danh sách phòng trống và chi tiết phòng gồm ảnh, tiện ích, giá dịch vụ, thiết bị | Đã làm |

### 4.4 Giấy tờ tùy thân

| Mã | Yêu cầu và tiêu chí kiểm tra | Mức |
| --- | --- | --- |
| FR14 | Customer tải lên giấy tờ CCCD; số CCCD được mã hóa khi lưu và luôn được che khi trả về | Một phần: backend và ứng dụng di động; chưa có màn hình web |
| FR15 | Manager (khu được giao) hoặc Admin duyệt hoặc từ chối giấy tờ | Một phần: backend và ứng dụng di động; chưa có màn hình web |

### 4.5 Lịch xem phòng

| Mã | Yêu cầu và tiêu chí kiểm tra | Mức |
| --- | --- | --- |
| FR16 | Customer gửi lịch xem cho phòng còn trống; mỗi phòng chỉ có một yêu cầu đang mở của cùng khách | Đã làm |
| FR17 | Admin duyệt và giao một Manager đang được phân công khu trọ, hoặc từ chối kèm ghi chú; khách nhận thông báo | Đã làm |

### 4.6 Đặt phòng và tiền cọc

| Mã | Yêu cầu và tiêu chí kiểm tra | Mức |
| --- | --- | --- |
| FR18 | Customer giữ chỗ phòng đang Available; không cho hai người giữ cùng một phòng (khóa dòng trong giao dịch) | Đã làm |
| FR19 | Tiền cọc bằng giá thuê một tháng; thời gian giữ chỗ chờ thanh toán là 15 phút | Đã làm |
| FR20 | Thanh toán cọc qua checkout payOS; dùng lại liên kết còn hạn; chống bấm hai lần; chỉ ghi nhận khi backend xác nhận qua webhook hoặc đối soát, không theo tham số trên URL trả về | Đã làm |
| FR21 | Trang kết quả thanh toán dùng chung cho tiền cọc và hóa đơn, tự kiểm tra lại trạng thái có giới hạn số lần | Đã làm |
| FR22 | Customer hủy booking chưa thanh toán; booking đã cọc qua payOS chỉ được giải phóng qua luồng hoàn cọc | Đã làm |

### 4.7 Hoàn cọc booking

| Mã | Yêu cầu và tiêu chí kiểm tra | Mức |
| --- | --- | --- |
| FR23 | Customer xin hoàn cọc cho booking đã cọc payOS và chưa có hợp đồng; nhập lý do, ngân hàng, số tài khoản (giữ số 0 đầu), tên chủ tài khoản; số tiền do backend xác định | Đã làm |
| FR24 | Mỗi booking chỉ có một yêu cầu đang xử lý; chỉ gửi lại sau khi bị từ chối; mất phản hồi khi gửi thì không tự gửi lại | Đã làm |
| FR25 | Admin xem hàng chờ, đối chiếu khoản cọc gốc và tài khoản, duyệt với số tiền không vượt yêu cầu hoặc từ chối kèm lý do | Đã làm |
| FR26 | Khi có yêu cầu hoàn chưa bị từ chối, không được lập hợp đồng từ booking đó | Đã làm |
| FR27 | Worker gửi payout cho yêu cầu đã duyệt; worker đối soát xác nhận kết quả; thành công thì booking chuyển Cancelled; timeout không được coi là thất bại | Một phần: đã có mã nguồn; kênh chi đang tắt trên môi trường triển khai |
| FR28 | Manager được phân công xem trạng thái và lịch sử hoàn cọc của booking ở chế độ chỉ đọc; khi yêu cầu hoàn đang xử lý thì không hiển thị thao tác lập hợp đồng và hủy booking | Đã làm |

### 4.8 Hợp đồng

| Mã | Yêu cầu và tiêu chí kiểm tra | Mức |
| --- | --- | --- |
| FR29 | Manager lập hợp đồng từ booking Deposited có khoản cọc thành công; ngày bắt đầu không trong quá khứ; ngày kết thúc sau ngày bắt đầu | Đã làm |
| FR30 | Giá thuê chốt theo giá phòng lúc lập; tiền cọc là số khách đã trả; phòng chuyển Occupied | Đã làm |
| FR31 | Gia hạn hợp đồng đang hiệu lực sang ngày kết thúc mới trong tương lai, không xung đột với booking khác | Đã làm |
| FR32 | Kết thúc hợp đồng khi khách đã bàn giao; nếu còn nợ hóa đơn phải xác nhận đã biết số nợ; chọn mở phòng ngay hoặc chuyển Maintenance rồi mở sau | Đã làm |
| FR33 | Customer xem hợp đồng, phòng đang thuê và lịch sử thuê; xem dòng thời gian hóa đơn theo kỳ | Đã làm |
| FR34 | Quy trình trả phòng đầy đủ: biên bản bàn giao, chỉ số cuối, quyết toán và hoàn cọc hợp đồng | Chưa làm |

### 4.9 Chỉ số điện nước

| Mã | Yêu cầu và tiêu chí kiểm tra | Mức |
| --- | --- | --- |
| FR35 | Manager ghi chỉ số theo tháng cho hợp đồng đang hiệu lực; chỉ số đầu kỳ tự nối từ chỉ số cuối kỳ trước | Đã làm |
| FR36 | Không cho chỉ số giảm, tháng tương lai, tháng ngoài thời hạn hợp đồng, hoặc ghi trùng hay lùi tháng | Đã làm |

### 4.10 Hóa đơn và thanh toán

| Mã | Yêu cầu và tiêu chí kiểm tra | Mức |
| --- | --- | --- |
| FR37 | Manager xem trước và phát hành hóa đơn tháng; xem trước và phát hành dùng cùng một cách tính | Đã làm |
| FR38 | Hóa đơn gồm tiền phòng, điện, nước theo chỉ số và dịch vụ cố định theo bảng giá khu trọ; mỗi dòng lưu đơn giá và mã giá tại thời điểm lập | Đã làm |
| FR39 | Mỗi hợp đồng chỉ có một hóa đơn mỗi tháng; tháng đầu được khấu trừ tiền cọc | Đã làm |
| FR40 | Customer thanh toán hóa đơn qua payOS; Manager ghi nhận tiền mặt; không ghi nhận hai lần cho một hóa đơn | Đã làm |
| FR41 | Khoản payOS đến sau khi hóa đơn đã trả được đưa vào hàng chờ đối soát; Admin kết luận đã hoàn tiền ngoài hệ thống (kèm mã chứng từ) hoặc không phải trả trùng | Đã làm |
| FR42 | Hóa đơn chưa trả quá hạn được hiển thị là quá hạn | Đã làm |
| FR43 | Tính phí theo số người và số xe | Chưa làm |
| FR44 | Nhắc hạn hóa đơn và hợp đồng sắp hết hạn | Chưa làm |

### 4.11 Khiếu nại hóa đơn

| Mã | Yêu cầu và tiêu chí kiểm tra | Mức |
| --- | --- | --- |
| FR45 | Customer khiếu nại một dòng cụ thể trên hóa đơn hoặc khiếu nại chung | Đã làm |
| FR46 | Admin hoặc Manager cập nhật trạng thái khiếu nại New, InProgress, Resolved; khách nhận thông báo | Đã làm |

### 4.12 Bảo trì thiết bị

| Mã | Yêu cầu và tiêu chí kiểm tra | Mức |
| --- | --- | --- |
| FR47 | Customer báo hỏng thiết bị của phòng thuộc hợp đồng của mình | Đã làm |
| FR48 | Admin giao yêu cầu cho Manager đang được phân công khu trọ (Pending sang Approved) | Đã làm |
| FR49 | Manager được giao nộp báo cáo hoàn thành kèm 1 đến 5 ảnh (Approved sang Completed); mọi thay đổi trạng thái được ghi lịch sử | Đã làm |

### 4.13 Thông báo, báo cáo và tổng quan

| Mã | Yêu cầu và tiêu chí kiểm tra | Mức |
| --- | --- | --- |
| FR50 | Hệ thống tạo thông báo cho các sự kiện booking, lịch xem, hợp đồng, hóa đơn, khiếu nại, bảo trì; đẩy realtime qua SignalR; người dùng đánh dấu đã đọc | Đã làm |
| FR51 | Báo cáo doanh thu theo thời điểm thu tiền và công nợ phải thu trên màn hình Báo cáo; Manager chỉ thấy khu được giao. Báo cáo tiền cọc booking (`/api/reports/booking-deposits`) mới có API, chưa có màn hình | Đã làm (báo cáo tiền cọc chỉ có API) |
| FR52 | Trang tổng quan quản lý: số phòng, tình trạng khu trọ, chỉ số chính | Đã làm |

### 4.14 Ứng dụng di động

| Mã | Yêu cầu và tiêu chí kiểm tra | Mức |
| --- | --- | --- |
| FR53 | Ứng dụng Flutter cho khách thuê: khám phá phòng trên bản đồ, booking, hợp đồng, hóa đơn, bảo trì, giấy tờ, thông báo | Một phần: còn dùng dữ liệu mẫu khi lỗi hoặc ở chế độ demo |
| FR54 | Ứng dụng Flutter cho quản lý: phòng, hợp đồng, ghi chỉ số, hóa đơn, duyệt giấy tờ, công việc bảo trì | Một phần: còn dùng dữ liệu mẫu; chưa có gia hạn, kết thúc hợp đồng, thanh toán payOS |

<!-- PAGE -->
# 5 Ca sử dụng chính

### UC01 Giữ chỗ và đặt cọc qua payOS

| Bước | Tác nhân | Hành động |
| --- | --- | --- |
| 1 | Customer | Chọn phòng trống, bấm Giữ phòng và thanh toán cọc |
| 2 | Hệ thống | Kiểm tra phòng còn cho thuê trong giao dịch có khóa, tạo booking Pending hạn 15 phút, thông báo cho Manager |
| 3 | Customer | Bấm Thanh toán qua payOS |
| 4 | Hệ thống | Tạo hoặc dùng lại yêu cầu thanh toán, lưu ngữ cảnh trong tab, chuyển sang checkout payOS |
| 5 | payOS | Khách chuyển khoản; payOS gửi webhook về backend và chuyển khách về trang kết quả |
| 6 | Hệ thống | Xác minh chữ ký webhook, ghi khoản thu, chuyển booking sang Deposited; trang kết quả đọc trạng thái từ backend và báo Đặt cọc thành công |

Ngoại lệ: quá 15 phút thì booking hết hạn và phòng được nhả; payOS chưa sẵn sàng thì báo lỗi và cho thử lại; liên kết bị hủy hoặc hết hạn thì không ghi nhận tiền.

### UC02 Xin và xét duyệt hoàn cọc

| Bước | Tác nhân | Hành động |
| --- | --- | --- |
| 1 | Customer | Mở booking đã cọc, bấm Xin hoàn cọc, nhập lý do và tài khoản nhận |
| 2 | Hệ thống | Kiểm tra booking đủ điều kiện, tạo yêu cầu PendingReview, chặn lập hợp đồng từ booking này |
| 3 | Admin | Mở Xét duyệt hoàn cọc, đối chiếu khoản cọc gốc và tài khoản, duyệt số tiền hoặc từ chối kèm lý do |
| 4 | Hệ thống | Duyệt: worker gửi payout và đối soát đến khi thành công, booking chuyển Cancelled. Từ chối: khách thấy lý do và có thể gửi lại |

### UC03 Lập hợp đồng

| Bước | Tác nhân | Hành động |
| --- | --- | --- |
| 1 | Manager | Chọn booking Deposited thuộc khu được giao, nhập ngày bắt đầu và kết thúc |
| 2 | Hệ thống | Khóa booking và phòng, kiểm tra khoản cọc, phân công, phòng chưa có hợp đồng hiệu lực, không có yêu cầu hoàn đang xử lý |
| 3 | Hệ thống | Tạo hợp đồng Active, chốt giá thuê và tiền cọc, phòng chuyển Occupied, thông báo cho khách |

### UC04 Ghi chỉ số và phát hành hóa đơn

| Bước | Tác nhân | Hành động |
| --- | --- | --- |
| 1 | Manager | Chọn hợp đồng và tháng; nhập chỉ số cuối kỳ (lần đầu nhập cả chỉ số đầu kỳ) |
| 2 | Hệ thống | Kiểm tra chỉ số, lấy danh sách dịch vụ theo loại phòng và giá theo bảng giá khu trọ, dựng các dòng hóa đơn |
| 3 | Manager | Xem trước và phát hành |
| 4 | Hệ thống | Lưu hóa đơn Unpaid (tháng đầu trừ tiền cọc), thông báo cho khách |

Ngoại lệ: dịch vụ chưa có giá trong tháng hoặc chưa ghi chỉ số thì không phát hành được.

### UC05 Thanh toán hóa đơn

| Bước | Tác nhân | Hành động |
| --- | --- | --- |
| 1 | Customer | Mở hóa đơn, bấm Thanh toán qua payOS |
| 2 | payOS | Khách chuyển khoản, payOS gửi webhook |
| 3 | Hệ thống | Hóa đơn chuyển Paid; nếu hóa đơn đã trả trước đó thì đưa khoản mới vào hàng chờ đối soát cho Admin |
| 3a | Manager | Thay thế: ghi nhận khách trả tiền mặt tại khu trọ |

### UC06 Báo hỏng và xử lý bảo trì

| Bước | Tác nhân | Hành động |
| --- | --- | --- |
| 1 | Customer | Từ Phòng của tôi, chọn thiết bị và mô tả hư hỏng |
| 2 | Admin | Giao yêu cầu cho Manager phụ trách khu trọ |
| 3 | Manager | Sửa chữa, nộp báo cáo hoàn thành kèm ảnh |
| 4 | Hệ thống | Ghi lịch sử từng bước, thông báo cho khách |

### UC07 Kết thúc hợp đồng

| Bước | Tác nhân | Hành động |
| --- | --- | --- |
| 1 | Manager | Mở hợp đồng, chọn Kết thúc, xác nhận khách đã bàn giao |
| 2 | Hệ thống | Tính tổng nợ hóa đơn; nếu còn nợ yêu cầu Manager xác nhận đã biết |
| 3 | Hệ thống | Hợp đồng chuyển Terminated; phòng chuyển Available hoặc Maintenance theo lựa chọn |
| 4 | Manager | Sau khi dọn dẹp, đánh dấu phòng sẵn sàng cho thuê |

<!-- PAGE -->
# 6 Quy tắc nghiệp vụ

| Mã | Quy tắc |
| --- | --- |
| BR01 | Phòng chỉ được giữ chỗ khi đang Available, không có booking Pending còn hạn, không có booking Deposited chưa kết thúc, không có hợp đồng Active |
| BR02 | Tiền cọc bằng giá thuê một tháng tại thời điểm giữ chỗ; booking Pending hết hạn sau 15 phút |
| BR03 | Tiền online chỉ được ghi nhận khi backend xác minh webhook hoặc đối soát với payOS; tham số trên URL trả về không phải bằng chứng thanh toán |
| BR04 | Booking đã cọc qua payOS không bị hủy trực tiếp; chỉ được giải phóng bằng hoàn cọc thành công hoặc lập hợp đồng |
| BR05 | Mỗi booking có tối đa một hợp đồng; mỗi phòng có tối đa một hợp đồng Active |
| BR06 | Mọi thao tác ghi của Manager kiểm tra lại phân công ngay trong giao dịch; bị thu hồi phân công thì mất quyền ngay |
| BR07 | Mỗi hợp đồng có tối đa một hóa đơn mỗi tháng; tiền cọc khấu trừ vào tiền phòng tháng đầu, không vượt tiền phòng |
| BR08 | Dòng hóa đơn lưu đơn giá, đơn vị, mã giá tại thời điểm lập; đổi giá sau này không làm đổi hóa đơn cũ |
| BR09 | Bảng giá của một dịch vụ trong một khu trọ không được có kỳ chồng lấn; ngày hiệu lực là đầu tháng, ngày kết thúc là mốc loại trừ |
| BR10 | Hóa đơn hiện chỉ tính điện theo kWh, nước theo m³ và phí cố định theo phòng |
| BR11 | Duyệt hoàn cọc không đồng nghĩa đã chuyển tiền; chỉ khi payout được xác nhận thành công mới là Refunded |
| BR12 | Số tài khoản ngân hàng chỉ hiển thị đầy đủ cho Admin khi xét duyệt; các nơi khác luôn che, chỉ hiện 4 số cuối |

<!-- PAGE -->
# 7 Mô hình trạng thái

| Đối tượng | Các trạng thái |
| --- | --- |
| Phòng | Available, Reserved, Occupied, Maintenance |
| Booking | Pending (chờ cọc, hạn 15 phút), Deposited (đã cọc), Cancelled |
| Hợp đồng | Active (hiển thị thêm Upcoming, Active, ExpiredPendingCheckout theo ngày), Terminated |
| Hóa đơn | Unpaid, Paid, Overdue (suy ra khi quá hạn chưa trả) |
| Yêu cầu thanh toán | Creating, Pending, Succeeded, Cancelled, Expired, Create-failed, RequiresReview |
| Yêu cầu hoàn cọc | PendingReview, Approved, Refunding, Refunded, Rejected, RequiresReview |
| Lần chi payout | Creating, Processing, Succeeded, Create-failed, Transfer-failed, RequiresReview |
| Lịch xem phòng | Pending, Approved, Rejected |
| Giấy tờ tùy thân | Pending, Approved, Rejected |
| Khiếu nại hóa đơn | New, InProgress, Resolved |
| Yêu cầu bảo trì | Pending, Approved, Completed |

### 7.1 Dữ liệu cốt lõi

| Nhóm dữ liệu | Thực thể |
| --- | --- |
| Người dùng | Users, Roles, IdentityDocuments, PropertyManagerAssignments |
| Khu trọ và phòng | Property, PropertyAmenities, PropertyServiceRates, RoomTypes, RoomTypeServices, Rooms, RoomImages, RoomAmenities, Amenities, Service, Equipments |
| Thuê | ViewingSchedules, Bookings, Contracts, UtilityReadings |
| Tài chính | Invoices, InvoiceDetails, Payments, PaymentRequests, PaymentWebhookReceipts, PaymentReviewCases, BookingRefundRequests, BookingRefunds, PayoutAttempts |
| Vận hành | MaintenanceRequests, MaintenanceReports, MaintenanceReportImages, MaintenanceLogs, InvoiceComplaints, Notifications |

<!-- PAGE -->
# 8 Giao diện và tích hợp

### 8.1 Màn hình web

| Nhóm | Màn hình |
| --- | --- |
| Công khai | Trang chủ có bản đồ, Danh sách phòng trống, Chi tiết khu trọ, Chi tiết phòng, Đăng nhập, Đăng ký |
| Customer | Đặt lịch xem, Đặt cọc, Kết quả thanh toán, Lịch xem và đặt cọc (kèm hoàn cọc), Hợp đồng của tôi, Phòng của tôi (kèm báo hỏng), Hóa đơn của tôi (kèm thanh toán và khiếu nại), Thông báo, Hồ sơ |
| Manager và Admin | Tổng quan, Khu trọ (kèm bảng giá dịch vụ), Phòng, Thiết bị, Lịch xem, Đặt phòng và đặt cọc, Hợp đồng, Lập hợp đồng, Hóa đơn và khiếu nại, Bảo trì, Báo cáo |
| Chỉ Admin | Người dùng, Quản lý Manager và phân công, Xét duyệt hoàn cọc, Đối soát thanh toán |

### 8.2 Nhóm API chính

| Nhóm | Đường dẫn |
| --- | --- |
| Xác thực, người dùng | /api/auth, /api/users, /api/roles |
| Công khai | /api/public/properties, /api/public/rooms |
| Khu trọ, phòng | /api/properties (kèm service-rates, managers), /api/rooms, /api/room-types, /api/amenities, /api/services, /api/equipments |
| Thuê | /api/viewing-schedules, /api/bookings (kèm payment-link, refund-requests), /api/contracts, /api/me/rentals |
| Tài chính | /api/utility-readings, /api/invoices, /api/payment-requests, /api/payment-reviews, /api/booking-refund-requests, /api/payments/payos/webhook, /api/reports |
| Vận hành | /api/identity-documents, /api/maintenance-requests, /api/invoice-complaints, /api/notifications, /hubs/notifications |

### 8.3 Tích hợp payOS

- Kênh thu: tạo liên kết checkout cho tiền cọc và hóa đơn; ReturnUrl và CancelUrl trỏ về trang kết quả của frontend; webhook công khai tại /api/payments/payos/webhook được xác minh chữ ký.
- Đối soát thu: worker định kỳ hỏi payOS về các yêu cầu còn treo để không sót giao dịch khi webhook không đến.
- Kênh chi: dùng khóa riêng; tách cờ bật gửi tiền và cờ bật đối soát để có thể vận hành an toàn từng bước.

<!-- PAGE -->
# 9 Yêu cầu phi chức năng

| Mã | Yêu cầu | Hiện thực |
| --- | --- | --- |
| NFR01 | Xác thực và phân quyền | JWT 120 phút; phân quyền theo vai trò ở mọi controller; frontend chặn route theo vai trò |
| NFR02 | Chống lạm dụng | Giới hạn tần suất: đăng nhập 5 lần mỗi phút, đăng ký và đặt phòng 10 lần mỗi phút |
| NFR03 | Bảo vệ dữ liệu cá nhân | Số CCCD mã hóa khi lưu, che khi trả về; số tài khoản ngân hàng được che; log không ghi mật khẩu, token, CCCD |
| NFR04 | Toàn vẹn dữ liệu | Khóa dòng trong giao dịch cho booking, hợp đồng, thanh toán; cập nhật có điều kiện chống ghi hai lần |
| NFR05 | Thanh toán an toàn | Xác minh chữ ký webhook; chỉ mở checkout trên miền pay.payos.vn; không tin tham số URL trả về |
| NFR06 | Bảo mật cơ sở dữ liệu | Frontend không truy cập cơ sở dữ liệu trực tiếp, mọi dữ liệu đi qua backend; bảng khóa bảo vệ dữ liệu (DataProtectionKeys) bật RLS và thu hồi quyền các vai trò Supabase |
| NFR07 | Hiệu năng giao diện | Tải lười từng màn hình; phân trang danh sách lớn; hủy request cũ khi đổi bộ lọc |
| NFR08 | Khả năng giám sát | Log có cấu trúc cho request, xác thực, thanh toán; endpoint kiểm tra sức khỏe ứng dụng và cơ sở dữ liệu |
| NFR09 | Kiểm thử | Backend khoảng 1.390 ca kiểm thử xUnit; frontend khoảng 320 ca kiểm thử Vitest; CI chạy kiểm thử khi mở pull request |
| NFR10 | Triển khai | Docker; Railway tự deploy từ nhánh main; migration EF chạy trước khi deploy |
| NFR11 | Khả dụng ngôn ngữ và định dạng | Giao diện tiếng Việt; tiền tệ định dạng vi-VN; ngày giờ theo múi giờ Việt Nam |

<!-- PAGE -->
# 10 Giới hạn hiện tại và rủi ro

| Mã | Giới hạn | Ảnh hưởng và hướng xử lý |
| --- | --- | --- |
| L01 | Chưa có quyết toán khi trả phòng; hợp đồng đã kết thúc không ghi được chỉ số và hóa đơn mới | Hóa đơn tháng cuối phải lập trước khi kết thúc hợp đồng; cần bổ sung FR34 |
| L02 | Tiền cọc đồng thời là tiền phòng tháng đầu; không có cọc đảm bảo để cấn trừ khi trả phòng | Cần thống nhất chính sách cọc (OD01) trước khi làm FR34 |
| L03 | Không tính tiền theo ngày khi vào hoặc ra giữa tháng | Hệ thống tính trọn tháng và hiển thị ghi chú; cần chốt OD02 |
| L04 | Giá dịch vụ không thời hạn không thể thay bằng giá mới vì kỳ chồng lấn | Khi tạo giá nên đặt tháng kết thúc; cần API đóng kỳ giá cũ |
| L05 | Giấy tờ tùy thân chưa là điều kiện để đặt cọc hoặc lập hợp đồng; chưa có màn hình web | Cần chốt OD04 và bổ sung màn hình |
| L06 | Lịch xem và bảo trì phụ thuộc Admin duyệt hoặc giao việc; khách chưa tự hủy được; bảo trì chưa có trạng thái từ chối | Có thể ùn tắc khi Admin vắng mặt |
| L07 | Kênh chi payOS đang tắt; hoàn cọc dừng ở trạng thái đã duyệt | Bật khi có khóa kênh chi riêng và đã kiểm thử với số tiền nhỏ |
| L08 | Ứng dụng di động còn dữ liệu mẫu và thiếu một số luồng | Không dùng ứng dụng di động làm căn cứ nghiệm thu luồng thanh toán |

<!-- PAGE -->
# 11 Quyết định còn mở

| Mã | Quyết định cần chốt | Ảnh hưởng |
| --- | --- | --- |
| OD01 | Tiền cọc là tiền giữ chỗ cộng tiền phòng tháng đầu, hay là cọc đảm bảo được hoàn khi trả phòng | FR34, BR07, L02 |
| OD02 | Tháng đầu và tháng cuối tính trọn tháng hay theo ngày; cách làm tròn | FR38, L03 |
| OD03 | Có yêu cầu báo trước khi trả phòng không; bao nhiêu ngày | FR34, FR44 |
| OD04 | Có bắt buộc giấy tờ đã duyệt trước khi đặt cọc hoặc lập hợp đồng không | FR14, FR29 |
| OD05 | Mức hoàn cọc booking và ai chịu phí chuyển tiền | FR25, FR27 |
| OD06 | Manager có được tự duyệt lịch xem và nhận việc bảo trì trực tiếp không | FR17, FR48, L06 |

<!-- PAGE -->
# 12 Truy vết yêu cầu

| Yêu cầu | API | Màn hình web |
| --- | --- | --- |
| FR18 đến FR22 | /api/bookings, /api/bookings/{id}/payment-link, /api/payment-requests/{id} | Đặt cọc, Kết quả thanh toán, Lịch xem và đặt cọc |
| FR23 đến FR28 | /api/bookings/{id}/refund-requests, /api/booking-refund-requests | Lịch xem và đặt cọc, Xét duyệt hoàn cọc, Đặt phòng và đặt cọc |
| FR29 đến FR33 | /api/contracts, /api/me/rentals | Lập hợp đồng, Hợp đồng, Hợp đồng của tôi, Phòng của tôi |
| FR10 | /api/properties/{id}/service-rates, /api/services | Khu trọ (chi tiết, bảng giá dịch vụ) |
| FR35 đến FR42 | /api/utility-readings, /api/invoices, /api/payment-reviews | Hóa đơn, Hóa đơn của tôi, Đối soát thanh toán |
| FR45 đến FR49 | /api/invoice-complaints, /api/maintenance-requests | Hóa đơn, Bảo trì, Phòng của tôi |
| FR50 đến FR52 | /api/notifications, /hubs/notifications, /api/reports | Thông báo, Tổng quan, Báo cáo |
