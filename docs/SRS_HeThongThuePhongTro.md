# Đặc tả yêu cầu phần mềm
## Hệ thống quản lý thuê phòng trọ

Software Requirements Specification • Phiên bản 1.0 • Ngày 12 tháng 11 năm 2026

Tài liệu mô tả yêu cầu của hệ thống quản lý thuê phòng trọ dành cho khách thuê, người quản lý khu trọ và quản trị viên. Phạm vi trọng tâm bao gồm tìm và đặt phòng, đặt cọc, lập hợp đồng, ghi điện nước, lập và thanh toán hóa đơn, kết thúc hợp đồng và trả phòng. Các chức năng quản lý khu trọ, dịch vụ, thông báo và báo cáo hỗ trợ xuyên suốt vòng đời thuê.

Mục tiêu là thống nhất nghiệp vụ, làm căn cứ thiết kế, triển khai và nghiệm thu. Mỗi yêu cầu chức năng có mã định danh và mức độ hiện thực để tránh hiểu phần dự kiến là chức năng đã hoàn thành. Các yêu cầu trả phòng là phạm vi cần bổ sung trước khi hệ thống có thể quản lý trọn vẹn một kỳ thuê.

### Thông tin tài liệu

| Thuộc tính | Nội dung |
| --- | --- |
| Mã tài liệu | SRS RENTAL 01 |
| Phiên bản | 1.0 |
| Đối tượng sử dụng | Nhóm phát triển, người hướng dẫn, người kiểm thử và bên quản lý vận hành |
| Phạm vi sản phẩm | Ứng dụng web quản lý nhiều khu trọ và nhiều phòng |
| Ngôn ngữ nghiệp vụ | Tiếng Việt |
| Đơn vị tiền tệ | Đồng Việt Nam VND |
| Múi giờ nghiệp vụ | Asia/Ho_Chi_Minh |
| Trạng thái phê duyệt | Chờ thống nhất các quyết định tại mục 17 |

### Cách đọc yêu cầu

H là yêu cầu đã có xử lý trong mã nguồn được đối chiếu. P là yêu cầu đã có một phần, còn thiếu tích hợp hoặc luồng nghiệp vụ. D là yêu cầu đề xuất bổ sung. Các ký hiệu này mô tả mức hiện thực, không xác nhận cấu hình môi trường thực tế đã bật chức năng.

Must là điều kiện cần để nghiệm thu phạm vi tương ứng. Should là yêu cầu nên có sau các chức năng bắt buộc. Các yêu cầu D và mục tiêu phi chức năng cần được thống nhất trước khi đưa vào cam kết triển khai.

### Lịch sử phiên bản

1.0 — Đặc tả vòng đời thuê và thanh toán; bổ sung yêu cầu trả phòng, quyết toán và các điểm cần quyết định.

<!-- PAGE -->
# 1 Giới thiệu và phạm vi

### 1 1 Mục tiêu sản phẩm

Hệ thống giúp khách thuê tìm phòng, giữ chỗ và theo dõi nghĩa vụ thanh toán; giúp Manager vận hành phòng thuộc khu trọ được giao; giúp Admin quản lý chính sách giá, tài khoản thu tiền, báo cáo và xử lý các ngoại lệ tài chính. Dữ liệu đặt cọc, hợp đồng, chỉ số điện nước và hóa đơn được liên kết để giảm nhập liệu lặp và tránh ghi nhận tiền trùng.

### 1 2 Phạm vi bao gồm

- Quản lý thông tin khu trọ, loại phòng, phòng, hình ảnh, tiện ích và thiết bị.
- Đăng ký, đăng nhập và phân quyền theo Customer, Manager và Admin.
- Xem phòng công khai, đăng ký lịch xem, duyệt lịch và đặt phòng.
- Thanh toán cọc booking, lập hợp đồng từ booking đã cọc và xem phòng đang thuê.
- Cấu hình giá dịch vụ theo khu trọ và kỳ áp dụng; ghi điện nước; xem trước và tạo hóa đơn.
- Thanh toán hóa đơn bằng payOS hoặc ghi nhận tiền mặt; đối soát và xử lý trả trùng.
- Xem công nợ, hóa đơn quá hạn và doanh thu theo thời điểm thu tiền.
- Yêu cầu trả phòng đúng hạn, kiểm tra bàn giao, quyết toán và hoàn cọc hợp đồng theo phạm vi bổ sung.

### 1 3 Giới hạn phạm vi

Tài liệu không đặc tả hệ thống kế toán tổng hợp, hóa đơn điện tử thuế, xác thực chữ ký số hợp đồng, giao dịch mua bán bất động sản, chia tiền tự động cho từng chủ trọ hoặc trả góp hóa đơn. Tiền cọc booking được quản lý riêng với doanh thu hóa đơn. Chức năng bảo trì và khiếu nại hóa đơn là mô đun liên quan, không được đặc tả đầy đủ trong phiên bản này.

Chấm dứt hợp đồng trước hạn, gia hạn tự động, khách ở quá hạn và xử lý tranh chấp phức tạp cần đặc tả riêng. Luồng trả phòng ở đây ưu tiên hợp đồng đến hạn, không tự suy rộng thành chính sách phạt hoặc tịch thu cọc.

### 1 4 Giả định và phụ thuộc

Mỗi phòng thuộc một khu trọ; quyền vận hành của Manager được xác định theo OwnerId hiện tại của khu trọ. Một tài khoản Customer có thể thuê nhiều phòng nếu từng phòng đáp ứng điều kiện. Mỗi phòng chỉ có một hợp đồng Active tại một thời điểm theo kiểm tra hiện tại.

Toàn bộ khoản thu online đi vào một tài khoản payOS của Admin. Kênh thu và kênh chi hộ dùng cấu hình riêng. Việc chạy online phụ thuộc kết nối provider, khóa hợp lệ và URL webhook truy cập được. Thanh toán giả lập chỉ phục vụ kiểm thử hoặc tương thích cũ, không được mô tả như thanh toán tiền thật.

<!-- PAGE -->
# 2 Thuật ngữ và tác nhân

### 2 1 Thuật ngữ nghiệp vụ

| Thuật ngữ | Ý nghĩa |
| --- | --- |
| Property | Khu trọ gồm nhiều phòng; có Manager được giao quản lý |
| RoomType | Loại phòng; cung cấp thông tin phân loại và giá thuê tham chiếu |
| Booking | Lần giữ chỗ của Customer cho một phòng trước khi lập hợp đồng |
| Cọc booking | Khoản tiền giữ phòng đã thu, được chuyển tiếp vào thông tin cọc của hợp đồng khi lập hợp đồng |
| Contract | Thỏa thuận thuê gắn với phòng, Customer, thời hạn, giá thuê và tiền cọc |
| Service rate | Giá dịch vụ của một khu trọ trong một khoảng tháng áp dụng |
| Invoice | Hóa đơn theo hợp đồng và tháng, gồm tiền phòng và các khoản phí |
| Payment request | Yêu cầu checkout online; chưa đồng nghĩa đã thu tiền |
| Payment | Bản ghi khoản thu thành công được hệ thống xác nhận |
| Refund request | Đơn đề nghị hoàn tiền có quyết định phê duyệt nghiệp vụ |
| Payout attempt | Một lần thực hiện chi hộ; tách khỏi quyết định duyệt hoàn |
| Quyết toán trả phòng | Chốt nghĩa vụ còn lại, bàn giao và tính số tiền cần thu hoặc hoàn |

### 2 2 Tác nhân

| Tác nhân | Trách nhiệm |
| --- | --- |
| Khách vãng lai | Tìm và xem thông tin khu trọ, phòng công khai |
| Customer | Đặt lịch xem, booking, thanh toán, xem hợp đồng và yêu cầu trả phòng của mình |
| Manager | Duyệt lịch, lập hợp đồng, ghi chỉ số, lập hóa đơn và bàn giao trong khu trọ đang quản lý |
| Admin | Quản trị toàn hệ thống; cấu hình giá; kiểm soát ngoại lệ thu và hoàn tiền |
| payOS | Tạo checkout, thông báo giao dịch, cung cấp trạng thái và dịch vụ chi hộ theo cấu hình |
| Tác vụ hệ thống | Đối soát, tổng hợp báo cáo và các tác vụ nhắc hạn được bổ sung |

### 2 3 Quyền đối với dữ liệu

Customer chỉ được xem và thao tác các booking, hợp đồng, hóa đơn và yêu cầu thanh toán của chính mình. Biết ID của đối tượng không tạo ra quyền truy cập. Manager bị giới hạn theo khu trọ đang được giao; khi đổi người quản lý, Manager mới nhận quyền và Manager cũ mất quyền theo quan hệ hiện tại. Admin có quyền toàn hệ thống nhưng các hành động tài chính vẫn phải tuân thủ trạng thái, chống trùng và lưu vết.

<!-- PAGE -->
# 3 Tổng quan luồng thuê và phân quyền

### 3 1 Vòng đời chính

| Bước | Người thực hiện | Kết quả nghiệp vụ |
| --- | --- | --- |
| 1 Tìm phòng | Khách hoặc Customer | Xem khu trọ, loại phòng và phòng đủ điều kiện |
| 2 Xem thực tế | Customer và Manager | Gửi và duyệt lịch xem nếu có nhu cầu |
| 3 Giữ chỗ | Customer | Booking Pending có hạn thanh toán 15 phút |
| 4 Đặt cọc | Customer và payOS | Khoản thu hợp lệ; booking Deposited giữ phòng |
| 5 Lập hợp đồng | Manager hoặc Admin | Chốt thời hạn, giá thuê và tiền cọc; phòng Occupied |
| 6 Vận hành kỳ thuê | Manager | Nhập điện nước, xem trước và phát hành hóa đơn |
| 7 Thu tiền | Customer hoặc Manager | payOS hoặc tiền mặt; hóa đơn Paid và có payment |
| 8 Chuẩn bị trả phòng | Customer và Manager | Yêu cầu trả phòng, lịch bàn giao và chỉ số cuối |
| 9 Quyết toán | Manager và Admin | Xác nhận nợ, khoản khấu trừ và phần cọc cần hoàn |
| 10 Kết thúc | Manager và hệ thống | Ghi nhận trả phòng; kiểm tra phòng trước khi mở cho thuê lại |

Bước 8 đến 10 là phần đề xuất. Lịch xem được duyệt không tự tạo booking và không phải điều kiện bắt buộc đang được kiểm tra khi tạo booking. Checkout thành công trên trình duyệt không tự tạo hợp đồng.

### 3 2 Ma trận quyền cốt lõi

| Chức năng | Customer | Manager | Admin |
| --- | --- | --- | --- |
| Tạo booking và link cọc | Của mình | Không | Không |
| Xem booking và hợp đồng | Của mình | Khu được giao | Toàn bộ |
| Lập hợp đồng | Không | Khu được giao | Toàn bộ |
| Tạo giá dịch vụ theo khu | Không | Không | Có |
| Ghi chỉ số và tạo hóa đơn | Không | Khu được giao | Toàn bộ |
| Thanh toán online | Hóa đơn mình | Không | Không |
| Ghi nhận tiền mặt | Không | Khu được giao | Toàn bộ |
| Xem báo cáo | Không | Khu được giao | Toàn bộ |
| Bàn giao trả phòng đề xuất | Xác nhận phần mình | Lập và xử lý | Giám sát |
| Duyệt hoàn tiền đề xuất | Gửi đơn | Cung cấp hồ sơ | Phê duyệt và thực hiện |

<!-- PAGE -->
# 4 Quy tắc đặt phòng và hợp đồng

### 4 1 Các quy tắc bắt buộc

| Mã | Quy tắc |
| --- | --- |
| BR01 | Chỉ cho đặt phòng Available và không bị booking còn hiệu lực hoặc hợp đồng chặn thuê |
| BR02 | Booking Pending giữ chỗ 15 phút; BE phải từ chối thanh toán quá hạn |
| BR03 | Tiền cọc booking hiện bằng giá thuê tháng tại lúc tạo booking; số tiền do BE xác định |
| BR04 | Booking Deposited giữ phòng không giới hạn thời gian cho đến khi lập hợp đồng hoặc hoàn tất hủy theo nghiệp vụ |
| BR05 | Không áp dụng quy tắc tự nhả sau 7 ngày cho booking đã cọc; thời gian cũ trong trường dữ liệu không thay thế chính sách này |
| BR06 | Một booking chỉ được lập một hợp đồng; một phòng không có hai hợp đồng Active đồng thời |
| BR07 | Chỉ booking Deposited mới được lập hợp đồng; ngày bắt đầu không trước ngày hiện tại và ngày kết thúc phải sau ngày bắt đầu |
| BR08 | Hợp đồng chốt giá thuê theo giá phòng tại lúc lập; cọc hợp đồng lấy từ cọc booking đã thu |
| BR09 | Booking đã có hợp đồng không được hủy như booking chưa thuê; phải đi qua nghiệp vụ hợp đồng |
| BR10 | Một Customer có thể có nhiều hợp đồng; giới hạn được kiểm soát theo phòng, không tự đặt giới hạn một hợp đồng mỗi người |

### 4 2 Các trạng thái đang có

Booking có Pending, Deposited và Cancelled. Booking hết 15 phút có thể vẫn lưu Pending; trạng thái hiển thị hết hạn phải xét thêm ExpiryDate. Booking đã lập hợp đồng có thể vẫn lưu Deposited; phải xét thêm liên kết ContractId. API mở rộng nên trả trạng thái hiệu lực và hành động được phép, tránh buộc mọi màn hình suy luận khác nhau.

Hợp đồng hiện có trạng thái Active. Việc so sánh StartDate và EndDate được dùng để phân biệt phòng đang thuê và lịch sử thuê. Ngày EndDate vẫn thuộc kỳ thuê; ngày sau EndDate mới nằm ngoài kỳ thuê theo cách lọc hiện tại.

### 4 3 Hủy booking và hoàn cọc

Customer có thể hủy booking Pending theo điều kiện trạng thái và checkout đang tồn tại. Cọc payOS đã thu không được hủy trực tiếp để bỏ mất nghĩa vụ hoàn. Luồng mục tiêu là Customer gửi yêu cầu, Admin xét duyệt, hệ thống chi hộ và xác nhận kết quả trước khi kết thúc hủy. Chính sách trạng thái và cấu trúc dữ liệu đã có; API xử lý hoàn và gửi payout chưa được hoàn thiện tại mốc P5.

Đơn hoàn cọc booking chỉ áp dụng trước khi lập hợp đồng. Khi tiền cọc đã chuyển vào hợp đồng, phải quản lý trong quyết toán hợp đồng để tránh hoàn cùng một khoản hai lần.

<!-- PAGE -->
# 5 Quy tắc điện nước hóa đơn và thu tiền

| Mã | Quy tắc |
| --- | --- |
| BR11 | Admin tạo giá dịch vụ cho từng khu trọ và tháng áp dụng; Manager sử dụng giá đã có để lập hóa đơn |
| BR12 | Giá cùng khu và dịch vụ không được chồng khoảng hiệu lực; đầu và cuối khoảng là đầu tháng, cuối khoảng không bao gồm tháng đó |
| BR13 | Một hợp đồng có tối đa một hóa đơn tháng theo mô hình hiện tại; không tạo hóa đơn tháng tương lai hoặc ngoài kỳ hợp đồng |
| BR14 | Hạn thanh toán khi tạo hóa đơn không được trước ngày hiện tại theo lịch Việt Nam |
| BR15 | Lần ghi điện nước đầu cần chỉ số đầu; các lần sau nối từ chỉ số cuối lần trước. Chỉ số mới không thấp hơn chỉ số cũ |
| BR16 | Phí điện nước bằng lượng dùng nhân đơn giá hiệu lực. Phí cố định theo phòng có số lượng một |
| BR17 | Tiền phòng hiện tính trọn MonthlyRent của hợp đồng cho kỳ hóa đơn; chưa tự chia theo ngày |
| BR18 | Làm tròn từng dòng phí dịch vụ về VND nguyên theo AwayFromZero; tổng hóa đơn bằng tổng các dòng đã tính |
| BR19 | Hóa đơn lưu ảnh chụp giá, số lượng và nguồn giá; thay giá dịch vụ không làm đổi hóa đơn đã tạo |
| BR20 | Không đủ giá hoặc thiếu chỉ số bắt buộc thì không tạo hóa đơn; không âm thầm bỏ khoản phí |
| BR21 | BE tính số tiền phải thu. Customer không được tự truyền tổng tiền để thay đổi giá checkout |
| BR22 | Chỉ xác nhận thu sau bằng chứng provider hợp lệ hoặc nghiệp vụ thu tiền mặt được cấp quyền; redirect không là bằng chứng |
| BR23 | Cùng một nghĩa vụ không được ghi payment thành công hai lần; trả trùng cần đưa vào hồ sơ Admin xử lý |
| BR24 | Thu tiền mặt phải xử lý link online đang hoạt động; không ghi Paid khi chưa biết checkout cũ có bị thu tiền hay không |
| BR25 | Báo cáo doanh thu hóa đơn gồm tiền phòng và phí dịch vụ; phân kỳ theo PaidAtUtc quy đổi lịch Việt Nam |
| BR26 | Payment hóa đơn thiếu thời điểm thu phải được thống kê riêng tổng tiền và số giao dịch; không tự gán thời điểm để đủ báo cáo |

### 5 1 Phạm vi tính phí hiện tại

Đã có nhánh tính theo sử dụng cho kWh và m³, và phí cố định theo phòng. Cấu hình giá chấp nhận đơn vị theo người hoặc xe, nhưng hóa đơn hiện từ chối hai cách tính này do chưa có số lượng làm căn cứ. Internet, rác hoặc giữ xe chỉ được tính khi dịch vụ áp dụng có cấu hình tương thích; không mặc định mọi phòng đều phát sinh tất cả khoản phí.

### 5 2 Quá hạn và báo cáo

Công nợ là nghĩa vụ hóa đơn chưa thanh toán; quá hạn phải xét ngày đến hạn cùng trạng thái thu tiền. Hết hạn thanh toán không tự hủy hóa đơn và không tự cấn tiền cọc. Báo cáo doanh thu đang mô tả khoản thu hóa đơn, không phải lợi nhuận kế toán hoặc doanh thu thuần sau hoàn tiền.

<!-- PAGE -->
# 6 Yêu cầu chức năng tìm phòng và booking

Các yêu cầu sau có ưu tiên Must, trừ FR10 là Should. H, P và D có ý nghĩa như phần đầu tài liệu.

| Mã | Yêu cầu và kết quả cần đạt | Mức |
| --- | --- | --- |
| FR01 | Cho khách xem danh sách, chi tiết khu trọ và phòng công khai; hỗ trợ các bộ lọc hệ thống cung cấp | H |
| FR02 | Hiển thị giá thuê, diện tích, vị trí, loại phòng và ảnh nếu có; không hiển thị dữ liệu cá nhân khách thuê trên màn hình công khai | H |
| FR03 | Cho đăng ký Customer và đăng nhập để nhận phiên truy cập phù hợp vai trò | H |
| FR04 | Cho Customer đăng ký lịch xem phòng; Manager đúng khu hoặc Admin được duyệt hay từ chối | H |
| FR05 | Tạo booking cho phòng đủ điều kiện; lưu Customer, phòng, tiền cọc và thời hạn 15 phút | H |
| FR06 | Khi có hai yêu cầu cùng đặt một phòng, chỉ cho một booking giữ chỗ hợp lệ theo điều kiện tại lúc ghi | H |
| FR07 | Danh sách và chi tiết booking phải lọc theo người đăng nhập; Manager không thấy booking ngoài khu được giao | H |
| FR08 | Tạo link payOS cho booking của chính Customer, còn hạn và chưa cọc; tái sử dụng yêu cầu thích hợp đang có | H |
| FR09 | Ghi nhận cọc từ webhook hoặc đối soát hợp lệ; cập nhật payment và Deposited có kiểm soát đồng thời | H |
| FR10 | Trả rõ trạng thái hiệu lực Hết hạn hoặc Đã lập hợp đồng và các hành động được phép bên cạnh trạng thái lưu trong DB | D |
| FR11 | Chặn hủy trực tiếp cọc payOS đã thu hoặc booking đã có hợp đồng; không giải phóng phòng dựa trên thao tác UI đơn lẻ | H |
| FR12 | Cho Customer tạo đơn hoàn cọc trước hợp đồng, Admin duyệt và theo dõi kết quả chi; xử lý lặp không hoàn hai lần | P |

### 6 1 Điều kiện nghiệm thu đặc biệt

Việc tạo lịch xem được duyệt không được tự thu tiền hoặc đổi booking thành Deposited. Booking Pending quá hạn không được dùng để tạo hợp đồng. Booking Deposited vẫn giữ phòng sau bảy ngày nếu chưa có hành động nghiệp vụ kết thúc hợp lệ.

FR08 và FR09 mô tả năng lực BE đã triển khai; kênh booking payOS còn phụ thuộc cờ bật và tích hợp màn hình ở môi trường sử dụng. FR12 mới có nền tảng trạng thái, dữ liệu và gateway đọc số dư, ước tính; chưa phải luồng hoàn tiền hoàn chỉnh.

<!-- PAGE -->
# 7 Yêu cầu chức năng hợp đồng và hóa đơn

Các yêu cầu FR13 đến FR24 có ưu tiên Must. FR25 và FR49 có ưu tiên Should và là chức năng mở rộng.

| Mã | Yêu cầu và kết quả cần đạt | Mức |
| --- | --- | --- |
| FR13 | Manager trong khu hoặc Admin lập hợp đồng từ booking Deposited chưa có hợp đồng; kiểm tra thời hạn hợp lệ | H |
| FR14 | Lưu giá thuê và cọc tại thời điểm lập, liên kết booking; đổi phòng sang Occupied cùng nghiệp vụ tạo hợp đồng | H |
| FR15 | Customer xem hợp đồng, phòng đang thuê và lịch sử của mình; hiển thị thông tin theo ngày thuê và quyền truy cập | H |
| FR16 | Admin tạo giá dịch vụ theo khu; từ chối khoảng tháng chồng lấn và cặp đơn vị không hợp lệ | H |
| FR17 | Admin và Manager đúng khu xem giá dịch vụ, bao gồm giá theo tháng được yêu cầu | H |
| FR18 | Manager hoặc Admin ghi chỉ số điện nước của hợp đồng; chống trùng kỳ và nối tiếp chỉ số | H |
| FR19 | Xem trước hóa đơn bằng cùng quy tắc tính dùng khi tạo trên cùng dữ liệu đầu vào; phản ánh tiền phòng, lượng dùng, giá và các khoản phí | H |
| FR20 | Tạo hóa đơn Unpaid, kiểm tra tháng và hạn, chống trùng hợp đồng và tháng; gửi thông báo trong hệ thống | H |
| FR21 | Customer chỉ xem hóa đơn của mình; Manager chỉ xem trong khu đang quản lý | H |
| FR22 | Tổng tiền do BE tính từ chi tiết; giữ nguyên giá đã chốt sau khi đơn giá dịch vụ thay đổi | H |
| FR23 | Từ chối tạo khi thiếu chỉ số, giá hoặc loại phí chưa được bộ tính hỗ trợ; thông báo nguyên nhân rõ ràng | H |
| FR24 | Không phát hành trùng kỳ ngay cả khi hai phiên tạo đồng thời hoặc người dùng bấm xác nhận nhiều lần | H |
| FR25 | Bổ sung số người, số xe theo kỳ và tính phí tương ứng; có căn cứ số lượng và nguồn giá trên dòng hóa đơn | D |
| FR49 | Nếu dữ liệu tính tiền thay đổi sau xem trước, yêu cầu người lập xác nhận lại tổng mới trước khi tạo hóa đơn | D |

### 7 1 Giao diện bắt buộc về nội dung

Màn hình tạo hóa đơn phải cho chọn hợp đồng và tháng; hiển thị chỉ số đã có hoặc hướng dẫn nhập bổ sung; cho xem trước từng khoản và tổng tiền. Khi server từ chối vì dữ liệu thay đổi, màn hình phải cho tải lại thay vì giữ thông báo tạo thành công cũ.

Customer phải thấy rõ phòng, khu trọ, tháng hóa đơn, hạn thanh toán, trạng thái, chi tiết khoản thu và tổng tiền. Không dùng dữ liệu mock để thay thế hóa đơn thật trong luồng thanh toán vận hành.

<!-- PAGE -->
# 8 Yêu cầu chức năng thanh toán và báo cáo

Các yêu cầu FR26 đến FR35 có ưu tiên Must trong phạm vi thanh toán và báo cáo. FR36 có ưu tiên Should.

| Mã | Yêu cầu và kết quả cần đạt | Mức |
| --- | --- | --- |
| FR26 | Customer tạo link payOS cho hóa đơn chưa trả của mình, số tiền hợp lệ và do BE xác định | H |
| FR27 | Lưu payment request, mã đơn, link, số tiền và đích Invoice hoặc Booking; mỗi yêu cầu chỉ liên kết một loại đích | H |
| FR28 | Cho xem trạng thái payment request theo quyền; trình duyệt đọc trạng thái server sau khi quay về checkout | H |
| FR29 | Webhook phải xác minh chữ ký, mã đơn, loại tiền, số tiền và trạng thái; từ chối bằng chứng không hợp lệ | H |
| FR30 | Ghi payment, PaidAtUtc, trạng thái Paid hoặc Deposited, Succeeded và thông báo trong một transaction nghiệp vụ | H |
| FR31 | Xử lý webhook lặp theo nguyên tắc idempotent; không tạo payment hoặc thông báo tài chính lặp | H |
| FR32 | Đối soát với provider khi thiếu webhook, mất phản hồi hoặc kẹt Creating; chưa rõ kết quả thì không tự coi thất bại | H |
| FR33 | Manager đúng khu hoặc Admin ghi nhận tiền mặt; phối hợp hủy checkout và chống ghi thu trùng | H |
| FR34 | Nhận diện giao dịch thừa, sai hoặc không thể áp dụng; lập hồ sơ để Admin xem xét, không cộng trùng vào doanh thu | H |
| FR35 | Báo cáo công nợ và quá hạn, doanh thu theo tháng và khu; thống kê riêng payment thiếu thời điểm thu | H |
| FR36 | Nhắc hóa đơn đến hạn và hợp đồng sắp hết hạn theo thời điểm được cấu hình | D |

### 8 1 Trạng thái yêu cầu thanh toán

Creating là đang tạo hoặc chưa biết kết quả tạo. Pending là link đang chờ thanh toán. Succeeded là đã áp dụng khoản thu thành công. Cancelled và Expired lần lượt biểu thị yêu cầu đã hủy và hết hạn theo bằng chứng thích hợp. Create-failed là thất bại khi tạo, không đồng nghĩa một giao dịch đã chuyển tiền thất bại. RequiresReview là cần kiểm tra nghiệp vụ.

### 8 2 Ràng buộc về báo cáo

Doanh thu hóa đơn bao gồm điện, nước, internet và dịch vụ đã thu trên hóa đơn. Khoản tiền thừa đang chờ Admin xử lý không được ghi thành một payment thu hóa đơn thứ hai. Payment chưa xác định thời gian phải có tổng tiền và số lượng riêng. Quyền xem báo cáo dựa trên Manager hiện tại của khu trọ.

<!-- PAGE -->
# 9 Ca sử dụng đặt phòng và lập hợp đồng

### UC01 Đặt phòng và đặt cọc

Tác nhân chính là Customer; tác nhân hỗ trợ là Manager khi có lịch xem và payOS khi thu cọc. Điều kiện đầu vào: Customer đã đăng nhập, phòng đủ điều kiện thuê và chưa bị giao dịch khác giữ chỗ hợp lệ.

1. Customer xem chi tiết phòng; có thể đặt lịch xem và chờ Manager duyệt.
2. Customer chọn đặt phòng. BE kiểm tra khả dụng và tạo Pending trong cửa sổ 15 phút.
3. Customer chọn thanh toán cọc. BE kiểm tra người sở hữu, hạn, trạng thái và số tiền; tạo hoặc dùng lại checkout thích hợp.
4. Customer thanh toán trên payOS. Khi quay về, FE hiển thị chờ xác nhận nếu BE chưa ghi nhận.
5. Webhook hoặc đối soát xác nhận khoản thu đúng. BE ghi payment, chuyển Deposited và phát thông báo.
6. Customer thấy đã cọc; phòng tiếp tục được giữ để Manager lập hợp đồng.

Kết quả thành công: một khoản cọc hợp lệ, một booking Deposited và không có booking cạnh tranh giữ cùng phòng. Không tự tạo hợp đồng trong bước thanh toán.

Ngoại lệ: phòng đã được người khác giữ thì từ chối tạo; hết 15 phút thì chặn checkout mới; provider timeout thì kiểm tra lại trước khi tạo lại. Khoản tiền đến muộn chỉ được áp dụng sau khi kiểm tra bằng chứng và quyền giữ phòng; nếu phòng đã chuyển cho người khác thì phải đưa vào xử lý ngoại lệ, không tự chiếm lại phòng.

### UC02 Lập hợp đồng từ booking

Tác nhân chính là Manager hoặc Admin. Điều kiện đầu vào: booking Deposited chưa có hợp đồng và người xử lý có quyền trên khu trọ. Luồng hoàn cọc đang hoạt động cần được kiểm soát khi bổ sung API hoàn, để không vừa lập hợp đồng vừa hoàn cùng khoản tiền.

1. Người xử lý mở booking, kiểm tra Customer, phòng và cọc đã thu.
2. Nhập StartDate và EndDate; hệ thống kiểm tra ngày và điều kiện booking.
3. BE lấy giá thuê hiện tại và tiền cọc đã thu, tạo hợp đồng Active, liên kết booking và cập nhật phòng Occupied.
4. Customer nhận thông báo và xem hợp đồng, phòng của mình khi đến kỳ thuê.

Kết quả thành công: một hợp đồng cho booking, không phát sinh payment cọc mới. Nếu người khác đã lập hợp đồng hoặc trạng thái phòng thay đổi trong lúc xử lý, yêu cầu sau bị từ chối và phải tải lại dữ liệu.

<!-- PAGE -->
# 10 Ca sử dụng lập và thanh toán hóa đơn

### UC03 Ghi chỉ số và phát hành hóa đơn

Tác nhân chính là Manager hoặc Admin. Điều kiện đầu vào: hợp đồng Active trong phạm vi được quản lý; có giá dịch vụ phù hợp tháng cần lập.

1. Chọn hợp đồng, tháng hóa đơn và hạn thanh toán.
2. Nếu có dịch vụ theo sử dụng, ghi chỉ số điện nước; hệ thống nối chỉ số đầu từ kỳ trước hoặc yêu cầu nhập ở lần đầu.
3. Chọn xem trước. BE tính tiền phòng trọn tháng và các phí hỗ trợ theo giá hiệu lực.
4. Người xử lý kiểm tra chi tiết và xác nhận. BE tính lại bằng cùng quy tắc và kiểm tra trùng kỳ.
5. Hệ thống lưu Unpaid cùng các dòng giá đã chốt và gửi thông báo cho Customer.

Kết quả thành công: Customer thấy hóa đơn đúng hợp đồng, tháng và tổng tiền. Nếu giá thay đổi giữa xem trước và xác nhận, cần cho xem lại số tiền mới trước khi chốt theo yêu cầu bổ sung về kiểm soát phiên xem trước; không mặc định bản xem trước là giữ giá vĩnh viễn.

Ngoại lệ: thiếu chỉ số hoặc giá, tháng ngoài kỳ, hạn trong quá khứ, loại phí theo người hoặc xe chưa hỗ trợ thì dừng và báo nguyên nhân. Hai lần tạo cùng tháng không tạo được hai hóa đơn.

### UC04 Thanh toán hóa đơn

Tác nhân chính là Customer; Manager có thể xử lý phương án tiền mặt. Điều kiện đầu vào: hóa đơn chưa Paid, thuộc Customer và có tổng tiền hợp lệ.

1. Customer mở chi tiết và chọn payOS. BE lấy tổng tiền, tạo hoặc dùng lại yêu cầu phù hợp.
2. Customer thanh toán trên checkout; FE quay về màn hình kết quả và đọc trạng thái BE.
3. Webhook được xác minh; BE áp dụng khoản thu đúng vào hóa đơn trong transaction, ghi PaidAtUtc và Succeeded.
4. Customer và Manager tải lại thấy Paid; báo cáo có một giao dịch đúng kỳ thu.

Kết quả thành công: có đúng một payment áp dụng nghĩa vụ, hóa đơn Paid và số liệu báo cáo không tăng khi webhook gửi lặp.

Ngoại lệ: nếu Customer chọn trả tiền mặt, Manager phải xử lý link online trước khi ghi nhận. Nếu provider đã thu thì dùng khoản online đã xác nhận; nếu chưa biết kết quả thì chờ đối soát. Redirect báo thành công khi chưa có bằng chứng chỉ được hiển thị chờ xác nhận. Nếu khách chuyển dư sau khi đã Paid, hệ thống lập hồ sơ Admin xử lý, không thu hóa đơn lần hai.

<!-- PAGE -->
# 11 Ca sử dụng trả phòng khi hết hợp đồng

### UC05 Chuẩn bị và bàn giao phòng

Đây là luồng đề xuất bổ sung. Tác nhân chính là Customer và Manager; Admin giám sát quyết toán và duyệt khoản hoàn. Điều kiện đầu vào: hợp đồng của Customer đến hạn hoặc sắp đến hạn; chưa có hồ sơ trả phòng đang xử lý cho cùng kỳ thuê.

1. Customer gửi ngày dự kiến trả và lời nhắn; Manager có thể khởi tạo hồ sơ khi Customer không gửi yêu cầu.
2. Manager đúng khu xác nhận lịch bàn giao và kiểm tra ngày kết thúc hợp đồng. Hệ thống giữ phòng ở trạng thái không thể cho thuê tiếp trong khi chưa bàn giao.
3. Manager và Customer kiểm tra phòng, thiết bị, chìa khóa; lưu kết quả, ảnh hoặc ghi chú làm căn cứ khi có hư hỏng.
4. Manager nhập chỉ số điện nước cuối tại thời điểm bàn giao; hệ thống kiểm tra không thấp hơn chỉ số đã chốt.
5. Hệ thống lập bản quyết toán: khoản hóa đơn chưa trả, tiền kỳ cuối chưa tính, khấu trừ được phê duyệt và số cọc đang giữ. Không đưa một khoản phí đã có trên hóa đơn vào tính thêm lần nữa.
6. Customer xem và xác nhận nội dung; nếu không đồng ý, hồ sơ chuyển cần xử lý, chưa tự khấu trừ hay hoàn tiền.

Kết quả giai đoạn này: có biên bản bàn giao và quyết toán có thể kiểm tra lại, chưa mặc định hoàn tất hợp đồng hoặc đã chuyển tiền.

### UC06 Quyết toán và kết thúc thuê

1. Manager chốt hồ sơ bàn giao. Admin duyệt khoản khấu trừ và hoàn theo chính sách được thống nhất.
2. Nếu Customer còn phải trả thêm, hệ thống tạo nghĩa vụ phải thu đúng loại và theo dõi thanh toán; không ghi số tiền âm vào checkout payOS.
3. Nếu còn tiền cọc cần hoàn, hệ thống tạo hồ sơ hoàn cọc hợp đồng riêng, kiểm tra tài khoản đích và thực hiện theo luồng chi được phê duyệt.
4. Khi đủ bằng chứng đã thu hoặc hoàn, hệ thống đánh dấu quyết toán xong và kết thúc hợp đồng. Lỗi hoặc timeout payout phải chờ đối soát, không đánh dấu đã hoàn.
5. Manager kiểm tra tình trạng phòng. Phòng đủ điều kiện được mở Available; cần sửa chữa thì giữ Maintenance và không cho đặt.

Hết EndDate chỉ là hết thời hạn thuê, không phải bằng chứng khách đã rời phòng, đã trả nợ hoặc đã nhận lại cọc. Phải ghi riêng mốc hết hạn, bàn giao thực tế và hoàn tất tài chính. Các điều kiện mở phòng khi còn công nợ cần chốt tại mục 17.

<!-- PAGE -->
# 12 Yêu cầu bổ sung cho trả phòng và quyết toán

FR37 đến FR47 đều là D và có ưu tiên Must trong phạm vi trả phòng. FR48 là D và có ưu tiên Should.

| Mã | Yêu cầu và tiêu chí kiểm tra |
| --- | --- |
| FR37 | Customer gửi và xem hồ sơ trả phòng của hợp đồng mình; Manager chỉ xử lý hồ sơ trong khu được giao |
| FR38 | Mỗi hợp đồng chỉ có một hồ sơ trả phòng đang hoạt động; thao tác lặp không tạo hai hồ sơ |
| FR39 | Ghi riêng ngày hết hạn, ngày hẹn và thời điểm bàn giao thực tế; nhắc hết hạn không tự giải phóng phòng |
| FR40 | Lưu biên bản kiểm tra phòng, thiết bị, chìa khóa, người lập và xác nhận của Customer; lưu ý kiến khi có bất đồng |
| FR41 | Ghi chỉ số cuối và tính tiền kỳ cuối theo chính sách đã duyệt; không thu trùng phí đã phát hành |
| FR42 | Tổng hợp công nợ, khấu trừ hợp lệ và cọc đang giữ; lưu từng khoản cùng nguồn gốc và người duyệt |
| FR43 | Tách tiền cọc còn phải hoàn và tiền khách phải trả thêm; chỉ cho cấn trừ khi chính sách và xác nhận cho phép |
| FR44 | Quản lý hoàn cọc hợp đồng riêng với hoàn booking; không để cùng khoản cọc bị hoàn qua hai luồng |
| FR45 | Không xác nhận đã hoàn tiền khi chỉ gửi yêu cầu payout hoặc timeout; theo dõi từng lần gửi và bằng chứng chuyển thực tế |
| FR46 | Kết thúc hợp đồng và đóng hồ sơ theo điều kiện quyết toán được duyệt; lưu vết thay đổi và không xóa lịch sử |
| FR47 | Sau bàn giao, chỉ mở phòng khi đã kiểm tra đủ điều kiện; trạng thái chưa được nhận biết phải chặn cho thuê để xử lý |
| FR48 | Cho xuất bản quyết toán và biên bản bàn giao để Customer và Manager lưu đối chiếu |

### 12 1 Các thực thể dự kiến

Cần bổ sung hồ sơ trả phòng gắn ContractId, ngày dự kiến, ngày bàn giao thực tế, người xử lý và trạng thái; biên bản thiết bị và chỉ số cuối; quyết toán có các dòng công nợ, khấu trừ và cọc; hồ sơ hoàn cọc hợp đồng và các lần chi tương ứng. Đây là dữ liệu dự kiến, chưa phải tên bảng hoặc schema đã triển khai.

### 12 2 Công thức quyết toán đề xuất

Gọi C là cọc còn đang giữ; N là các nghĩa vụ còn phải trả đã được xác nhận; K là khấu trừ hợp lệ chưa tính trong N. Khi được phép cấn trừ: tiền hoàn = max(0, C - N - K); tiền phải thu thêm = max(0, N + K - C). Một khoản chỉ xuất hiện một lần trong N hoặc K. Nếu không được phép cấn trừ, phải theo dõi nợ và hoàn cọc riêng. Công thức này chờ phê duyệt, không phải logic hiện tại.

<!-- PAGE -->
# 13 Mô hình trạng thái và dữ liệu

### 13 1 Trạng thái trả phòng dự kiến

Hồ sơ trả phòng có thể đi qua Requested, Scheduled, Inspected, AwaitingSettlement và Completed; thêm Disputed khi có bất đồng. Tên trạng thái và hành động hủy yêu cầu cần được thống nhất khi thiết kế chi tiết. Completed chỉ được đặt sau các điều kiện bàn giao và tài chính được phê duyệt; đây là trạng thái hồ sơ, không thay thế trạng thái giao dịch ngân hàng.

Hợp đồng cần mở rộng ngoài Active để biểu diễn kết thúc thuê; có thể dùng Ended cùng ngày hết hạn và thời điểm bàn giao riêng. Không chỉ thêm một giá trị chuỗi rồi cập nhật DB: bộ lọc phòng khả dụng, lịch sử thuê, ghi chỉ số, lập hóa đơn và đối soát phải được cập nhật đồng bộ để nhận biết trạng thái mới.

### 13 2 Trạng thái hoàn cọc booking đã thiết kế

Đơn hoàn có PendingReview, Approved, Refunding, Refunded, Rejected và RequiresReview. Lần chi có Creating, Processing, Succeeded, Create-failed, Transfer-failed và RequiresReview. Approved chỉ là duyệt nghiệp vụ; Succeeded của lần chi cần bằng chứng chuyển tiền. Timeout không chứng minh đã thất bại và không cho phép gửi một lần chi mới tùy ý.

### 13 3 Dữ liệu cốt lõi và quan hệ

| Nhóm dữ liệu | Nội dung và quan hệ cần bảo toàn |
| --- | --- |
| Users và Roles | Danh tính, trạng thái tài khoản và vai trò |
| Property và Rooms | Khu trọ có OwnerId hiện tại; phòng thuộc một khu và loại phòng |
| Bookings | Liên kết Customer và phòng, số cọc, hạn thanh toán và trạng thái |
| Contracts | Liên kết booking, Customer và phòng; ngày thuê, giá thuê, cọc đã chốt |
| Services và PropertyServiceRates | Dịch vụ và giá theo khu, khoảng tháng, đơn vị, cách tính |
| UtilityReadings | Chỉ số điện nước theo hợp đồng và tháng, chuỗi đầu cuối |
| Invoices và InvoiceDetails | Hợp đồng, kỳ, hạn, trạng thái và chi tiết giá đã chốt |
| PaymentRequests và Payments | Đích Invoice hoặc Booking, mã provider; khoản thu thực tế và PaidAtUtc |
| BookingRefundRequests và PayoutAttempts | Quyết định hoàn cọc booking và từng lần chi, mã tham chiếu, trạng thái |
| Hồ sơ trả phòng dự kiến | Hợp đồng, biên bản, quyết toán, công nợ và hoàn cọc hợp đồng |

Không xóa dây chuyền dữ liệu tài chính hoặc hợp đồng khi người dùng ngừng hoạt động. Mã đơn, mã link và các mã giao dịch phải được kiểm soát duy nhất theo ngữ nghĩa của từng loại dữ liệu.

<!-- PAGE -->
# 14 Giao diện và tích hợp bên ngoài

### 14 1 Các màn hình nghiệp vụ

Customer cần trang tìm phòng và chi tiết, lịch xem, booking và cọc, hợp đồng hoặc phòng của tôi, danh sách và chi tiết hóa đơn, kết quả thanh toán, thông báo; bổ sung hồ sơ trả phòng và quyết toán. Manager cần danh sách phòng, lịch xem, booking, hợp đồng, nhập chỉ số, xem trước và tạo hóa đơn, ghi nhận tiền mặt, báo cáo và bàn giao. Admin cần quản trị khu trọ, giá dịch vụ, tài khoản, báo cáo toàn hệ thống và xử lý ngoại lệ tài chính.

Màn hình phải hiển thị rõ chờ tạo link, chờ xác nhận tiền, thành công, hết hạn hoặc cần kiểm tra. Sau thao tác thay đổi dữ liệu phải đọc lại kết quả server; tắt nút bấm không thay thế kiểm tra đồng thời tại BE.

### 14 2 Các API hiện có liên quan trực tiếp

| Nhóm | Phương thức và đường dẫn |
| --- | --- |
| Xem khu và phòng | GET /api/public/properties và /api/public/properties/{id}/rooms |
| Lịch xem | GET hoặc POST /api/viewingschedules; POST /api/viewingschedules/{id}/approve hoặc reject |
| Booking | GET hoặc POST /api/bookings; GET /api/bookings/{id}; POST /api/bookings/{id}/cancel |
| Cọc online | POST /api/bookings/{id}/payment-link |
| Hợp đồng | GET hoặc POST /api/contracts; GET /api/contracts/{id} |
| Phòng của tôi | GET /api/me/rentals và /api/me/rentals/history |
| Giá dịch vụ | GET hoặc POST /api/properties/{id}/service-rates |
| Điện nước | GET hoặc POST /api/utilityreadings |
| Hóa đơn | GET hoặc POST /api/invoices; POST /api/invoices/preview |
| Thu tiền hóa đơn | POST /api/invoices/{id}/payment-link hoặc /record-cash |
| Trạng thái và đối soát | GET /api/payment-requests/{id}; POST /api/payment-requests/{id}/reconcile |
| Webhook thu | POST /api/payments/payos/webhook |
| Báo cáo | GET /api/reports/revenue và /api/reports/receivables |

### 14 3 Ràng buộc tích hợp payOS

Backend giữ toàn bộ khóa; frontend chỉ nhận thông tin checkout cần dùng. Webhook phải được xác minh và chống lặp. Payout dùng cấu hình riêng; P5 mới hỗ trợ số dư và estimate-credit, chưa gửi tiền. EstimatedCredit không tự được coi là phí VND hoặc bằng chứng tên chủ tài khoản. Chưa có API trả phòng hoặc hoàn cọc hợp đồng được xác nhận trong phạm vi đối chiếu này.

<!-- PAGE -->
# 15 Yêu cầu phi chức năng

Các mục dưới đây là yêu cầu nghiệm thu đề xuất, không phải số liệu hiệu năng hoặc mức đáp ứng đã đo của hệ thống.

| Mã | Yêu cầu có thể kiểm tra | Ưu tiên |
| --- | --- | --- |
| NFR01 | Mọi API dữ liệu thuê riêng phải xác thực và kiểm tra quyền trên đối tượng; bộ kiểm thử phải có trường hợp đổi ID sang dữ liệu người khác | Must |
| NFR02 | Không đưa ClientId kèm khóa bí mật, API key, checksum key, mật khẩu hoặc token vào FE, response lỗi và log vận hành | Must |
| NFR03 | Trong thử nghiệm 20 yêu cầu đồng thời cùng booking hoặc hóa đơn, chỉ ghi một khoản thu áp dụng và một chuyển trạng thái thành công | Must |
| NFR04 | Gửi lặp cùng webhook hợp lệ 10 lần không tăng payment, tổng doanh thu hoặc số lần hoàn nghĩa vụ | Must |
| NFR05 | Cập nhật tài chính trong DB có tính nguyên tử; lỗi giữa chừng không để hóa đơn Paid mà thiếu payment hoặc ngược lại | Must |
| NFR06 | Lưu timestamp giao dịch theo UTC; kỳ tháng và ngày đến hạn xác định theo lịch Việt Nam; có test ranh giới ngày và tháng | Must |
| NFR07 | Khi provider timeout, lưu trạng thái có thể phục hồi; cấu hình timeout hữu hạn và không thực hiện retry gây chuyển tiền trùng | Must |
| NFR08 | Các hành động duyệt khấu trừ, hoàn tiền, ghi tiền mặt và đóng hồ sơ phải truy được người thực hiện, thời điểm và căn cứ | Must |
| NFR09 | Mục tiêu p95 dưới 2 giây cho API đọc thông thường với 100 người dùng đồng thời trên bộ dữ liệu kiểm thử thống nhất, không tính thời gian provider | Should |
| NFR10 | Các màn hình trọng tâm dùng được ở chiều rộng 360 px và desktop; lỗi nhập liệu phải gắn đúng trường, không chỉ thông báo chung | Should |
| NFR11 | Có cơ chế sao lưu và diễn tập phục hồi; mục tiêu ban đầu RPO 24 giờ, RTO 4 giờ, cần thống nhất theo hạ tầng sử dụng | Should |
| NFR12 | Kiểm thử tự động tách môi trường tiền thật; dữ liệu kiểm thử không chứa khóa thật và không gọi payout live | Must |

### 15 1 Khả năng bảo trì

Giữ tách lớp API, nghiệp vụ và lưu trữ; bộ tính tiền phải dùng chung cho xem trước và tạo. Thay đổi schema cần migration có thể kiểm tra; thay đổi trạng thái hợp đồng phải rà lại tất cả nơi dùng trạng thái. Khóa kênh thu và chi được cấp từ cấu hình môi trường hoặc kho bí mật, tách khỏi mã nguồn.

<!-- PAGE -->
# 16 Tiêu chí nghiệm thu và truy vết

Mỗi ca kiểm thử phải kiểm tra cả response, dữ liệu lưu và phạm vi quyền. Những dòng có yêu cầu D hoặc P là tiêu chí cho phần cần hoàn thiện, không khẳng định hiện tại đã chạy đạt.

| Mã | Tình huống và kết quả bắt buộc | Liên kết |
| --- | --- | --- |
| AT01 | Hai Customer cùng giữ một phòng; chỉ một booking có hiệu lực | FR05 FR06 BR01 |
| AT02 | Quá 15 phút, tạo link cọc mới bị chặn; đã cọc hơn 7 ngày vẫn giữ phòng | FR08 FR09 BR02 BR04 |
| AT03 | Customer hoặc Manager truy cập đối tượng ngoài quyền bị từ chối, không lộ chi tiết | FR07 FR15 FR21 NFR01 |
| AT04 | Tạo hợp đồng lặp từ cùng booking bị chặn; phòng thành Occupied và cọc không bị thu lại | FR13 FR14 BR06 BR08 |
| AT05 | Kỳ chỉ số sau nối đúng kỳ trước; chỉ số lùi và hóa đơn trùng tháng bị chặn | FR18 FR20 FR24 |
| AT06 | Ví dụ tiền phòng 3 triệu, điện 50 kWh giá 4000, nước 5 m³ giá 15000, internet 80000: tổng 3355000 VND | FR19 FR22 BR16 BR18 |
| AT07 | Chỉ đổi query redirect sang thành công không làm Paid; webhook sai chữ ký hoặc sai tiền không được áp dụng | FR28 FR29 BR22 |
| AT08 | Webhook đúng lặp nhiều lần chỉ có một payment và một lần tăng doanh thu | FR30 FR31 NFR04 |
| AT09 | Mất phản hồi tạo link hoặc thiếu webhook được phục hồi bằng đối soát, không ghi thu hai lần | FR32 NFR07 |
| AT10 | Tiền mặt khi checkout đang hoạt động phải xử lý link trước; khoản chuyển dư được đưa vào hồ sơ kiểm tra | FR33 FR34 BR24 |
| AT11 | Doanh thu gồm các phí đã thu; thiếu PaidAtUtc xuất hiện nhóm riêng; đổi Manager thì quyền xem đổi tương ứng | FR35 BR25 BR26 |
| AT12 | Hết EndDate nhưng chưa bàn giao không làm phòng Available | FR39 FR47 UC05 |
| AT13 | Quyết toán không tính lại phí đã có; Customer thấy từng khoản; bất đồng chặn quyết toán tự động | FR40 FR41 FR42 |
| AT14 | Hoàn cọc booking và hợp đồng không hoàn cùng tiền hai lần; payout timeout không bị ghi Refunded | FR12 FR44 FR45 |
| AT15 | Hoàn tất bàn giao và tài chính theo chính sách; phòng cần sửa vào Maintenance, phòng đủ điều kiện mới mở thuê | FR46 FR47 UC06 |
| AT16 | Giá hoặc số lượng đổi sau xem trước; người lập phải xác nhận lại tổng mới trước khi phát hành | FR49 FR19 |

### 16 1 Nguyên tắc phê duyệt

Phần có mức H chỉ được coi là nghiệm thu khi chạy trên môi trường kiểm thử có cấu hình phù hợp. Phần P và D cần hoàn thành ca sử dụng tương ứng, thử lỗi provider, thao tác đồng thời và kiểm tra không còn chênh lệch giữa trạng thái màn hình với DB. Không dùng kết quả giả lập để xác nhận chi hộ thực tế đã hoạt động.

<!-- PAGE -->
# 17 Quyết định còn mở và căn cứ đối chiếu

### 17 1 Những chính sách cần thống nhất

| Mã | Quyết định cần chốt | Ảnh hưởng |
| --- | --- | --- |
| OD01 | Có yêu cầu báo trả phòng trước không; nếu có là bao nhiêu ngày | FR36 FR37 FR39 |
| OD02 | Tiền phòng tháng đầu và cuối tính trọn tháng hay theo ngày; cách làm tròn và mốc chốt | FR41 BR17 |
| OD03 | Quyết toán bổ sung dùng loại chứng từ nào khi tháng cuối đã có hóa đơn; không phá quy tắc một hóa đơn tháng | FR41 FR42 |
| OD04 | Có được cấn cọc vào nợ không; ai duyệt từng loại khấu trừ và cần bằng chứng gì | FR42 FR43 |
| OD05 | Sau bàn giao có mở phòng khi khách còn nợ không; theo dõi nợ tách khỏi tình trạng vật lý thế nào | FR46 FR47 |
| OD06 | Thời hạn hoàn cọc, mức hoàn booking và cách xử lý phí chi hộ; không mặc định khách chịu phí | FR12 FR44 FR45 |
| OD07 | Có cần xác minh tên tài khoản đích bằng dịch vụ khác khi API hiện tại không trả kết quả này | FR44 FR45 |
| OD08 | Xác nhận bàn giao của Customer dùng thao tác trong web, OTP hay chứng từ ký tay | FR40 FR48 |
| OD09 | Có giới hạn số hợp đồng mỗi Customer hoặc cho phép thuê hộ và ở ghép không | BR10 FR25 |

Đề xuất ưu tiên triển khai: thống nhất OD02 đến OD06; hoàn thiện hồ sơ trả phòng và quyết toán; tiếp tục API hoàn cọc và gửi payout; sau đó bổ sung nhắc hạn và các loại phí theo người hoặc xe. Không dùng hoàn cọc booking để thay thế quyết toán hợp đồng.

### 17 2 Căn cứ chức năng hiện tại

- BookingService và RoomRentabilityPolicy: cửa sổ 15 phút, cọc theo giá thuê, giữ Deposited và điều kiện phòng được đặt.
- ContractService, ContractRepository và ContractStatuses: tạo từ Deposited, chốt giá, phòng Occupied và trạng thái Active.
- RentalService và RentalRepository: phòng đang thuê và lịch sử theo ngày Việt Nam, EndDate bao gồm ngày cuối.
- InvoiceService, UtilityReadingService và PropertyService: tính tiền, nối chỉ số, giá theo khu, chống trùng và giới hạn loại phí.
- Các controller Bookings, Contracts, Invoices, Properties, PaymentRequests, Reports và MyRentals: API và phạm vi vai trò.
- BookingRefundPolicy, BookingRefundRequestStatuses và PayoutAttemptStatuses: nền tảng hoàn cọc booking và trạng thái lần chi.
- Tài liệu payos-payout-channel trong thư mục PRN232_BE/docs: cấu hình chi riêng, số dư, estimate-credit và giới hạn xác minh tài khoản.

Các tên tệp trên tương ứng mã nguồn trong PRN232_BE tại thời điểm lập phiên bản 1.0. Tài liệu không chứa khóa tích hợp hoặc thông tin đăng nhập. Thay đổi quy tắc đã thống nhất cần cập nhật mã yêu cầu, tiêu chí nghiệm thu và phiên bản SRS tương ứng.
