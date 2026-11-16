# C7–C8: Xét duyệt và bổ sung hồ sơ sau cọc

## Giao diện

- Manager/Admin: menu **Hồ sơ CCCD đặt cọc**, `/management/booking-identities`.
- Customer: **Lịch xem & đặt cọc → Bổ sung & xem hồ sơ CCCD**, `/booking-profile?bookingId=...`.
- Booking cũ không có hồ sơ mới hiển thị hướng dẫn liên hệ quản lý, không tự tạo hồ sơ giả hoặc sửa dữ liệu lịch sử.

## Xét duyệt từng người

Danh sách phân trang 50 hồ sơ đã cọc, chỉ trong phạm vi hiện tại của Manager; Admin xem toàn bộ. CCCD dạng số luôn che, ảnh chỉ xem bằng link private tạm sau kiểm tra quyền; response NoStore. Truy cập ảnh là POST được audit, không log ảnh/signed URL/CCCD.

Chỉ xét duyệt người đang PendingReview. Duyệt yêu cầu đủ họ tên, số CCCD, xác nhận đồng ý và cả hai mặt hiện hành. Từ chối bắt buộc lý do <=500 ký tự; Customer thấy lý do trong trang hồ sơ. Mỗi quyết định lưu reviewer, thời điểm UTC, revision và application version vào ledger BookingApplicationReviews riêng, bảo vệ bằng RLS.

Sửa thông tin hoặc thay/xóa ảnh sau duyệt vô hiệu quyết định hiện hành và tăng revision. Hồ sơ đủ chuyển PendingReview, còn thiếu chuyển Incomplete. Thay số CCCD đã có sẽ bỏ hiệu lực cả hai mặt ảnh cũ để tránh dùng ảnh người cũ cho số CCCD mới; cần tải lại hai mặt. Lịch sử quyết định cũ vẫn giữ.

## Thay người/số người sau cọc

- Chỉ Customer sở hữu hồ sơ, booking Deposited và chưa có bất kỳ hợp đồng nào được bổ sung/sửa. Booking Pending, Cancelled hoặc đã có hợp đồng khóa sửa; các guard được kiểm tra lại trong transaction.
- Ngày dọn vào sau cọc giữ nguyên; thay ngày không thuộc C8.
- Số người trong hồ sơ là đề xuất, còn Booking.OccupantCount là số đã chốt. Tăng/giảm số người, hoặc thay họ tên/CCCD của người ở cùng đã khai báo, tạo đề xuất Pending. Bổ sung thông tin vào một slot trống đã có sẵn chỉ cần duyệt giấy tờ, không tự tăng số người.
- Giảm người cần xác nhận rõ; hồ sơ bị bỏ được gỡ khỏi đề xuất, lịch sử xét duyệt vẫn giữ. Chưa có quy trình tự xóa toàn bộ ảnh/lịch sử theo thời hạn (C10).
- Chỉ Manager hiện được phân công xác nhận danh sách. Trước khi duyệt danh sách, từng người phải được duyệt và đủ hồ sơ. Admin duyệt CCCD nhưng không thay vai trò Manager xác nhận danh sách.
- Từ chối danh sách giữ nguyên số người đã chốt trong booking. Đề xuất vẫn hiển thị là Rejected cùng lý do, không rollback âm thầm thông tin Customer đang đề xuất. Customer sửa hoặc gửi lại đề xuất; việc gửi lại không tự duyệt.
- Chỉ khi Manager Approved danh sách, số người booking mới cập nhật trong cùng transaction với quyết định/ledger. Tiền cọc, giá thuê, payment, hoàn tiền và hóa đơn không bị thay đổi bởi thao tác này.

## API

Giữ các API C4–C6; mở cập nhật người/ảnh cho hồ sơ Submitted đủ điều kiện, không mở sửa cho người khác.

Customer:

- `GET /api/booking-applications/by-booking/{bookingId}`.
- `POST /api/booking-applications/{id}/roster-resubmit`: `{ version }`, chỉ cho đề xuất Rejected.
- `PATCH /api/booking-applications/{id}`: sau cọc chỉ đề xuất số người, ngày phải giữ nguyên, vẫn có confirmRemovePeople và version.

Manager/Admin:

- `GET /api/booking-application-reviews?page=1` (50/trang).
- `GET /api/booking-application-reviews/{id}`.
- `POST /api/booking-application-reviews/{id}/people/{personId}/review`: `{ decision: Approved|Rejected, note, version }`.
- `POST /api/booking-application-reviews/{id}/people/{personId}/images/{Front|Back}/read-url`.
- `POST /api/booking-application-reviews/{id}/roster-review`: cùng body quyết định, **chỉ Manager**.

404 cho hồ sơ ngoài phạm vi, 409 cho phiên bản cũ/quyết định đã đổi/booking hoặc hợp đồng không còn cho sửa, 400 cho thiếu lý do hoặc quyết định không hợp lệ. FE đọc lại hồ sơ khi 409 và không tự gửi đè quyết định.

## Khóa và phân quyền

Ghi hồ sơ Submitted khóa Booking → Room → ManagerAssignment (FOR SHARE nếu Manager) → Application → các bản ghi con. Kiểm tra lại booking Deposited, chưa có hợp đồng và phân công hiện tại trước khi lưu. Việc thu hồi assignment phải đợi thao tác đang giữ shared lock hoàn tất; request mới sau thu hồi bị chặn.

Application/Person Version chống duyệt ảnh của revision cũ, concurrent upload và hai reviewer ghi đè nhau. Upload mất cuộc đua sẽ dọn object mới theo cơ chế C4. Sau khi ký ảnh, quyền Manager được kiểm tra lại trước trả URL; link đã cấp vẫn có hạn sử dụng 120 giây, không hứa thu hồi ngay mọi link cũ.

## Migration và giới hạn

Migration `AddBookingApplicationReviewWorkflow` thêm trạng thái đề xuất trên BookingApplications và bảng ledger BookingApplicationReviews. Hồ sơ đã tồn tại nhận RosterChangeStatus=None, không đổi tiền hoặc quyết định CCCD hiện có. Ledger có FK đến application/reviewer; PersonId là định danh lịch sử không FK để giữ lịch sử sau khi gỡ người khỏi đề xuất. RLS/revoke Data API được áp dụng cho bảng mới. Chưa chạy migration trên Supabase/DB của người dùng.

C9 vẫn cần nối điều kiện ReadyForContract và sao chép hồ sơ đã duyệt vào luồng lập hợp đồng. C7–C8 chưa thay đổi bắt buộc của API tạo hợp đồng cũ, chưa tự tạo hợp đồng và chưa có worker dọn dữ liệu C10. API tạo booking cũ vẫn tương thích như C4–C6.

## Kiểm thử

TestServer + SQLite + Storage/payment giả kiểm tra lý do từ chối, gửi lại, đổi revision/ảnh, stale reviewer, phạm vi Manager và thu hồi quyền, đề xuất danh sách được duyệt/từ chối, số tiền không đổi, khóa sau hợp đồng, model/migration và RLS SQL offline. Chưa kiểm thử Supabase/chi trả thật hoặc cạnh tranh PostgreSQL thực tế trong đợt này.

FE kiểm tra từ chối thiếu lý do, refresh sau 409 không ghi đè, quyền xác nhận danh sách, Customer sửa hồ sơ bị từ chối và trạng thái khóa khi đã có hợp đồng.

Kết quả: 56 test BE pass; 32 test FE hồ sơ/booking/menu pass; 2 Node test pass; TypeScript và production build FE pass. Lượt FE đầy đủ có 495/496 test pass, lỗi duy nhất là kỳ vọng menu chưa có mục CCCD mới. Đã cập nhật kỳ vọng và chạy lại toàn bộ file menu cùng 3 file workflow liên quan (32/32 pass); không chạy lại toàn bộ FE sau thay đổi kỳ vọng test này. Migration chỉ được sinh và kiểm tra SQL/model offline, chưa áp dụng DB của người dùng.
