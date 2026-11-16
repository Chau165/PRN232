# Hồ sơ CCCD booking — C9 và C10

## C9: lập hợp đồng từ hồ sơ đã duyệt

- Manager tải `GET /api/booking-application-reviews/by-booking/{bookingId}/contract-source`. Response chỉ chứa CCCD che số, phiên bản và `readyForContract`.
- Với booking có hồ sơ, FE hiển thị danh sách chỉ đọc; gửi `bookingApplicationVersion` khi `POST /api/contracts`. BE tự lấy họ tên/CCCD đã duyệt, bỏ qua các trường CCCD/occupants do client gửi.
- Chưa duyệt đủ hai mặt ảnh, roster chưa xác nhận, khác số người booking hoặc phiên bản cũ: 409. Quản lý mất phân công cũng không được lập hợp đồng.
- Repository kiểm tra lại nguồn dưới khóa Booking → Room → Assignment → Application, ghi hợp đồng, người ở, snapshot và chuyển hồ sơ `Converted` trong transaction hiện tại. Không tạo hợp đồng nếu bất kỳ bước kiểm tra nào thất bại.
- `ContractIdentitySnapshots` lưu họ tên, CCCD mã hóa, revision, ID/phiên bản nguồn và đường dẫn ảnh UUID bất biến. Đây là bản sao dữ liệu độc lập, dùng lại object ảnh private, không sao chép bytes. Không có API sửa snapshot. Cleanup bảo vệ đường dẫn snapshot.
- Booking cũ không có hồ sơ vẫn dùng form nhập trước đây. Đây không phải migration bắt buộc mọi client chuyển sang luồng mới; công việc khóa API cũ thuộc C11.
- Customer sở hữu, Manager đang được phân công, Admin xem danh sách tại `GET /api/contracts/{id}/identity-snapshots`; lấy ảnh qua `POST /api/contracts/{id}/identity-snapshots/{snapshotId}/images/{Front|Back}/read-url`. Dùng quyền hợp đồng hiện tại, kiểm tra lại sau khi ký URL, response no-store. URL sống 120 giây; FE đóng ảnh sau 110 giây và xóa trạng thái khi đổi tài khoản/hợp đồng.
- Giao diện xem hợp đồng Customer và Manager có nút “Xem hồ sơ CCCD đã chốt”. Hợp đồng cũ hiển thị chưa có snapshot.

## C10: audit và dọn dữ liệu theo chính sách

Mặc định không xóa dữ liệu: `Enabled=false`, `DryRun=true`, `DraftRetentionDays=null`. Chưa chốt thời hạn lưu nên không tự chọn ngày thay người vận hành.

Cấu hình riêng cho môi trường BE (ví dụ tên biến Railway; số ngày phải do chủ hệ thống quyết định):

- `BookingIdentityCleanup__Enabled`: bật/tắt worker.
- `BookingIdentityCleanup__DryRun`: true chỉ kiểm đếm; false mới thực thi.
- `BookingIdentityCleanup__DraftRetentionDays`: số ngày được phê duyệt, 1–3650. Bật worker mà thiếu số ngày sẽ báo cấu hình không hợp lệ.
- `BookingIdentityCleanup__BatchSize`: 1–100, mặc định 20.

Admin có `GET /api/booking-identity-retention/preview` xem số lượng (không lộ CCCD/path); `POST /api/booking-identity-retention/run` chạy một batch, vẫn tuân thủ Enabled/DryRun. Worker chạy mỗi giờ khi bật.

Chỉ chọn Draft chưa từng gắn Booking và quá hạn dựa trên UpdatedAtUtc. Stage khóa hồ sơ, chuyển Cancelled, đổi Version; enqueue từng ảnh, chờ ít nhất 1 giờ. Ảnh bị bỏ do giảm người hoặc upload không ghi DB được cũng đưa vào hàng đợi bền vững. Upload có kết quả không rõ sau khi gửi Storage giữ path nội bộ để enqueue, không trả path cho client.

Worker claim task bằng lease 5 phút; xóa có timeout 10 giây, retry tăng dần tối đa 1 ngày, tối đa 10 lần. Sau đó cần kiểm tra vận hành; task không biến mất. Khóa advisory theo object path phối hợp với thao tác gắn ảnh để chặn xóa/gắn đồng thời. Mỗi task có unique ObjectPath và conditional lease update, xóa Storage 404 là thành công idempotent.

Trước khi xóa luôn kiểm tra snapshot hợp đồng và ảnh thuộc hồ sơ đang dùng/đã liên kết booking. Các ảnh này được đánh dấu Retained, không xóa. Chỉ purge metadata của nháp Cancelled chưa liên kết sau khi mọi ảnh đã xóa thành công. Không tự xóa hồ sơ đã cọc, booking hủy, hồ sơ xét duyệt/từ chối, payment/refund hay bằng chứng hợp đồng: cần chính sách lưu riêng.

`BookingIdentityRetentionAudits` lưu ID, sự kiện và thời điểm; không lưu CCCD, URL ký, nội dung exception hay đường dẫn ảnh. Các endpoint đọc ảnh POST tiếp tục được audit bởi bộ lọc hiện có. Ba bảng mới bật RLS và thu hồi quyền API Supabase có kiểm tra role tồn tại; BE truy cập DB bằng vai trò phù hợp như các bảng riêng tư trước đó.

Giới hạn: chưa inventory Storage để tìm object thất lạc trước phiên bản này. Nếu DB hỏng đúng lúc enqueue thì cần kiểm tra Storage thủ công; không khẳng định có transaction phân tán giữa DB và Storage. Không có quy trình xóa dữ liệu hợp đồng đã hết hạn trong đợt này.

## Migration và kiểm thử

Migration: `20261116030926_AddContractIdentitySnapshotsAndCleanup`. Chỉ tạo bảng/index, không đổi dữ liệu thanh toán hay backfill snapshot cho hợp đồng cũ. Chưa chạy cập nhật DB thật. Sau khi xem migration, chạy từ PRN232_BE trong môi trường có connection string đúng:

```powershell
dotnet ef database update --project PRN232.Infrastructure --startup-project PRN232.Api
```

Kiểm thử tự động: tích hợp API trên SQLite (phân quyền, hồ sơ chưa duyệt, stale version, snapshot độc lập/che số, ảnh được bảo vệ), SQL migration PostgreSQL/RLS/model drift, hàng đợi cleanup (disabled/dry-run, chờ grace, retry lỗi Storage, chống enqueue trùng, bảo vệ ảnh đang dùng và purge sau xóa), FE form hợp đồng và signed preview. Chưa chạy thao tác xóa ảnh thật hoặc thử khóa nhiều tiến trình trên PostgreSQL production.

Kết quả kiểm tra đợt này: 53 test BE liên quan pass; 21 test trang hợp đồng và 2 test preview pass; TypeScript và Vite build pass. Bộ FE đầy đủ: 500/501 pass; một test hộp thoại ảnh bảo trì không tìm thấy dialog trong thời gian chờ. File bảo trì chạy lại riêng pass 10/10; chưa thay đổi code bảo trì trong đợt này.
