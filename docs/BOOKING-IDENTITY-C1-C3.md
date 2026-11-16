# C1–C3: Hồ sơ CCCD trước khi đặt cọc

## Phạm vi và quy tắc C1

C1–C3 chỉ bổ sung nền tảng, chưa mở API upload hoặc thay đổi FE/luồng thanh toán. Không bắt buộc hồ sơ mới đối với booking cũ. Dữ liệu mẫu hoặc migration không tạo/xóa booking, payment hoặc hợp đồng hiện có.

- Người đặt (CustomerId từ token ở C4) là **Bạn**, Position=0, dự kiến đứng tên hợp đồng. Các vị trí 1..N là **Người ở cùng**. Số người bao gồm Bạn và không vượt sức chứa phòng.
- Chuẩn bị hồ sơ không giữ phòng. Chỉ sau bước submit C5 kiểm tra phòng, quyền và booking đang chờ thì mới tạo/liên kết booking và bắt đầu 15 phút.
- Bạn cần họ tên, đúng 12 chữ số CCCD, hai mặt ảnh hiện hành và xác nhận đồng ý cung cấp thông tin trước khi submit. Người ở cùng có thể thiếu hồ sơ lúc cọc; phải đủ và được duyệt trước khi lập hợp đồng.
- Application: Draft → Submitted → Converted; Draft/Submitted có thể Cancelled qua nghiệp vụ tương lai. Cancelled chỉ là trạng thái hồ sơ, không hủy booking hoặc hoàn tiền.
- Person: Incomplete → PendingReview → Approved/Rejected. Rejected bắt buộc lý do (500 ký tự). Thay thông tin/ảnh tăng revision, vô hiệu quyết định cũ; ảnh giữ lại phải được kiểm tra và liên kết rõ với revision mới. Không tự nhận diện CCCD thật hoặc OCR.
- Thanh toán và duyệt hồ sơ độc lập. Payment succeeded không duyệt CCCD; từ chối CCCD không hủy giao dịch đã thu. Bổ sung hồ sơ hoặc thực hiện quy trình hủy/hoàn riêng.
- Sau cọc, đổi người/số người cần Manager xác nhận ở C8. Sau lập hợp đồng, thay đổi qua luồng hợp đồng, không chỉnh ngược hồ sơ đã chuyển.
- Customer chỉ sửa bản nháp/hồ sơ của mình trong trạng thái được phép. Manager hiện được phân công và Admin mới được xét duyệt. C4/C7 phải kiểm tra quyền mỗi request, kể cả cấp signed URL; adapter Storage không phải điểm kiểm tra quyền.
- Không dùng việc thiếu ảnh làm lý do bỏ qua kiểm tra sức chứa, một booking Pending, tính sẵn có phòng hoặc quyền của Manager.

## C2: Mô hình và migration

- BookingApplications: chủ hồ sơ, phòng dự kiến, ngày vào, số người, trạng thái, liên kết booking nullable/unique, thời điểm, Version (optimistic concurrency).
- BookingApplicationPeople: vị trí unique trong hồ sơ, họ tên, CCCD mã hóa bằng converter/Data Protection hiện có, hash để dùng kiểm tra trùng ở C4, revision, trạng thái và quyết định duyệt, thời điểm ghi nhận sự đồng ý, Version.
- BookingIdentityImages: mỗi ảnh thuộc một người, Front/Back, đường dẫn object (không lưu URL ký), MIME, số byte, revision và IsCurrent. Unique một ảnh hiện hành/mặt/người và unique ObjectPath. Ảnh cũ được giữ riêng để C10 xử lý theo chính sách lưu trữ.
- FK Restrict; không cascade xóa ảnh/hồ sơ. Các ràng buộc DB kiểm tra trạng thái, mặt ảnh, dung lượng, số người và quyết định từ chối. Quy tắc liên bảng và hash trùng do service C4/C7 thực thi; chưa có API nên không có write path bỏ qua quyền.
- Migration AddBookingIdentityPreparation bật RLS và thu hồi quyền Data API của cả ba bảng, chỉ thực hiện REVOKE role Supabase khi role tồn tại. BE cần role kết nối DB phù hợp; không thêm policy mở cho anon/authenticated.
- Migration được tạo trong source, chưa áp dụng Supabase/local DB của người dùng. Chỉ chạy update khi người vận hành đã chọn đúng DB. Down sẽ xóa ba bảng mới, không dùng rollback khi đã có hồ sơ cần giữ.

## C3: Storage private

Cấu hình backend bằng User Secrets hoặc biến Railway (không đưa key vào FE/Git):

```text
Supabase__Url=https://<project-ref>.supabase.co
Supabase__ServiceRoleKey=<backend legacy service_role JWT>
Supabase__BookingIdentityBucket=booking-identities
```

Tạo bucket `booking-identities` với Public OFF, giới hạn 5 MiB và MIME image/jpeg,image/png,image/webp. Không dùng bucket property-images hoặc thêm policy cho trình duyệt trực tiếp ghi/xem CCCD. Adapter hiện tương thích legacy service_role JWT như các adapter Storage sẵn có; key sb_secret cần cập nhật cách gửi Authorization trước khi sử dụng.

Adapter kiểm tra metadata bucket phải private trước upload/cấp link. Nếu metadata thiếu, bucket public hoặc gọi Supabase lỗi thì từ chối. Chỉ nhận ảnh <=5 MiB, kiểm tra chữ ký file theo MIME; kiểm tra này không thay thế full decoder, chống malware hoặc xác minh giấy tờ. Đường dẫn ngẫu nhiên không chứa họ tên/CCCD/tên file khách gửi. Không upsert ghi đè ảnh cũ.

Link xem có hiệu lực 120 giây; kiểm tra URL trả về cùng host/đường dẫn ký, không chấp nhận URL public/host lạ. Signed URL là quyền xem tạm cho bất kỳ ai có link đến khi hết hạn; đăng xuất hoặc thu hồi quyền Manager không lập tức vô hiệu link đã cấp. Không ghi link/token, byte ảnh, số CCCD hoặc key vào log. API C4/C7 phải NoStore và audit xem ảnh không chứa dữ liệu nhạy cảm.

C3 chưa tạo bucket thật hoặc chạy upload thật vì chưa có cấu hình bucket riêng được xác nhận. Không thay đổi appsettings.json hiện người dùng đang sửa.

## Chính sách lưu trữ còn cần chốt

Chưa bật worker xóa tự động. Cần quyết định số ngày giữ bản nháp bỏ dở, hồ sơ hủy/từ chối và ảnh thay thế; thời hạn giữ bằng chứng đã lập hợp đồng phải có chính sách riêng. Không mặc định xóa bằng chứng hợp đồng/payment hoặc giữ vô thời hạn. C10 bổ sung cleanup và audit sau khi quyết định các mốc này.

## Kiểm tra

Kết quả: 25 test pass (9 test mới cho nền tảng hồ sơ/Storage và 16 test hồi quy CCCD/cọc). Model khớp migration, SQL migration được sinh offline để kiểm tra RLS/index. Build API cấu hình CccdPreparation pass; test project có một cảnh báo nullability sẵn có trong InvoiceOverdueStatusTests. Chưa chạy migration trên PostgreSQL/Supabase hoặc kiểm tra Storage thật.

BookingIdentityPreparationTests kiểm tra đủ hồ sơ chủ thuê, cho phép người ở cùng bổ sung, vô hiệu duyệt theo revision, mã hóa CCCD và model/migration, bucket public, object path, MIME/signature, signed URL/expiry. HTTP Storage được mô phỏng; chưa có kiểm thử Supabase thật hoặc API phân quyền C4/C7.

Tham khảo cơ chế bucket private và signed URL của Supabase: https://supabase.com/docs/guides/storage/buckets/fundamentals
