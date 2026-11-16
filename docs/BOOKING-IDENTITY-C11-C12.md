# CCCD booking — C11 kiểm thử và C12 triển khai

## Trạng thái và nguyên nhân upload

FE cũ khóa input file khi chưa lưu consent hoặc còn chỉnh thông tin. Vì vậy nút Choose file bị vô hiệu hóa ngay trên trình duyệt, chưa có request gửi BE. FE mới cho chọn ảnh khi đã tích consent, có họ tên và CCCD hợp lệ; tự lưu thông tin trước và dùng Version mới để upload. Giữ nút Lưu riêng cho người muốn kiểm tra trước. Upload lỗi sau khi lưu vẫn giữ Version đã lưu để thử lại. Áp dụng cả form chuẩn bị booking và bổ sung sau cọc. Đổi tài khoản/rời trang trong khi lưu thì không bắt đầu upload tiếp.

Ảnh Supabase cậu cung cấp ngày 16/11/2026 chưa có bucket `booking-identities`. Đây là bước cấu hình riêng: thiếu bucket gây lỗi Storage sau khi đã chọn file, không gây khóa hộp thoại chọn file. Không coi ảnh chụp là bằng chứng đã kiểm tra trực tiếp cấu hình cloud.

## C11: kiểm thử tự động

BE `BookingIdentityEndToEndTests` chạy API, SQLite transaction, Storage adapter thật với HTTP giả lập và bộ xác minh chữ ký payOS thật với gateway giả lập:

1. Customer chuẩn bị hồ sơ hai người, tải hai mặt của người đứng tên; chưa giữ phòng trước Submit.
2. Submit → booking Pending → tạo link payOS theo tiền server.
3. Webhook có chữ ký hợp lệ, gửi lại hai lần → chỉ một Payment, có PaidAtUtc, booking Deposited và request Succeeded.
4. Customer bổ sung hai mặt CCCD của người ở cùng.
5. Customer khác bị từ chối xem ảnh trước khi gọi Storage.
6. Manager duyệt đủ hai người; lập hợp đồng bằng Version đã duyệt.
7. Lưu hai snapshot độc lập, đủ bốn metadata ảnh/private objects; hồ sơ Converted; chủ hợp đồng xem ảnh bằng signed URL ngắn hạn.

Kết hợp các suite đã có để kiểm tra role/revocation, hồ sơ thiếu, stale Version, MIME/size, public bucket, URL khác host, retry cleanup và ảnh hợp đồng được bảo vệ. Test không dùng ảnh CCCD thật, tiền thật hay Supabase production.

Từ PRN232_BE:

```powershell
dotnet test --configuration Release --filter "FullyQualifiedName~BookingIdentityEndToEndTests|FullyQualifiedName~BookingApplicationApiTests|FullyQualifiedName~BookingApplicationReviewApiTests|FullyQualifiedName~ContractIdentityWorkflowTests|FullyQualifiedName~BookingIdentityPreparationTests|FullyQualifiedName~BookingPayOSApiTests"
```

Từ PRN232_FE:

```powershell
pnpm typecheck
pnpm test:booking-identity
pnpm build
```

FE kiểm tra lưu tự động trước upload, dùng Version mới, chặn upload nếu lưu lỗi, thử lại sau lỗi Storage, cô lập tài khoản, xét duyệt và màn hình hợp đồng/thanh toán. API client giữ multipart boundary do trình duyệt tạo.

## C12: cấu hình và triển khai

1. Trong đúng project Supabase → Storage → New bucket: tên `booking-identities`, Private (Public tắt), tối đa 5 MiB/ảnh, allowed MIME `image/jpeg,image/png,image/webp`. Không dùng bucket room-images/property-images public cho CCCD. Migration EF không tự tạo bucket Storage.
2. Variables ở **service BE Railway**, không phải FE:

```text
Supabase__Url=https://PROJECT_REF.supabase.co
Supabase__ServiceRoleKey=<key backend của đúng project, không đưa vào chat/git/FE>
Supabase__BookingIdentityBucket=booking-identities
```

Backend đang dùng header apikey và Bearer; cần loại key service_role tương thích cấu hình hiện tại, không dùng anon/publishable key. Với server service-role, không thêm policy public để chữa lỗi. Tham khảo chính thức: https://supabase.com/docs/guides/storage/security/access-control và https://supabase.com/docs/guides/storage/buckets/creating-buckets.

3. Kiểm tra connection string BE trỏ Supabase mong muốn. Sao lưu DB theo quy trình vận hành trước update. Từ PRN232_BE, chạy migration bằng cấu hình đúng môi trường:

```powershell
dotnet ef dbcontext info --project PRN232.Infrastructure --startup-project PRN232.Api
dotnet ef database update --project PRN232.Infrastructure --startup-project PRN232.Api
```

Không tạo lại migration. C11–C12 không thêm schema mới; cần đủ ba migration C1/C7/C9 có trong repo. Chạy SQL chỉ đọc `BOOKING-IDENTITY-C11-C12-PREFLIGHT.sql` trên Supabase SQL Editor để kiểm tra history, bảng và RLS. Health DB=200 chỉ xác nhận kết nối, không chứng minh migration đủ hay bucket đã tồn tại.

4. BE cần DataProtection key ring và cấu hình giải mã CCCD hiện tại ổn định giữa các lần deploy. Không reset key ring để xử lý lỗi upload. Giữ `BookingIdentityCleanup__Enabled=false` nếu chưa chốt chính sách lưu.
5. BE CORS cho phép đúng origin FE. FE `VITE_API_BASE_URL=https://prn232-production.up.railway.app/api`. Kiểm tra pipeline FE đọc biến lúc build; redeploy FE có bản sửa mới, BE có migration/API tương ứng. Không đưa Supabase ServiceRoleKey vào biến VITE.
6. PayOS dùng cấu hình thật hiện có: Enabled/BookingPaymentsEnabled, return/cancel về FE `/payment-result`, webhook về BE `/api/payments/payos/webhook`. Chuyển trình duyệt về FE không tự làm booking Deposited; phải có webhook/đối soát được xác minh.
7. Commit/push nhánh, chờ CI qua rồi merge theo quy trình repo; xem cả deploy BE/FE đạt Ready. Không coi code local là code đã xuất bản. Nếu rollback, rollback ứng dụng tương thích; không tự chạy migration Down xóa bảng hồ sơ.

## Preflight chỉ đọc, không đăng nhập

Từ thư mục gốc, cần Node 20+:

```powershell
node PRN232_BE/scripts/check-booking-identity-release.mjs
node --test PRN232_BE/scripts/check-booking-identity-release.test.mjs
```

Mặc định chỉ kiểm tra BE public ở URL người dùng cung cấp. Có thể đặt `IDENTITY_BE_URL` để dùng môi trường khác (HTTPS origin). Muốn kiểm tra bucket, cung cấp ba biến Supabase__* trong phiên terminal riêng rồi chạy thêm `--storage`. Script không in key, không upload ảnh, không ghi DB, không tạo checkout. Không lấy key từ FE. Nếu không truyền --storage thì bucket **chưa được xác minh**, dù các check công khai đều pass.

## UAT thật: chỉ đánh dấu hoàn tất sau khi có kết quả

| Bước | Người thao tác | Kết quả phải đối chiếu |
|---|---|---|
| 1 | Customer A | Chọn phòng trống dành cho UAT, chuẩn bị hồ sơ; nhập thông tin/consent rồi chọn ảnh. UI hiện Đã tải cho hai mặt; Network POST multipart 200. Không cần bấm Lưu riêng trước upload. |
| 2 | Người vận hành | Supabase private bucket có object theo application/person; DB có metadata, API chỉ trả CCCD che số, không trả object path/key. Không chia sẻ ảnh thật trong log. |
| 3 | Customer B/Manager khác khu | Không xem/ghi được hồ sơ A; không nhận signed URL. Manager đúng phân công xem được. |
| 4 | Customer A | Submit chỉ tạo một booking, bắt đầu hạn thanh toán; mở checkout và trả số tiền đã thống nhất cho lần UAT. Không dùng lại booking đã hết hạn. |
| 5 | Customer/BE | payOS xác nhận → webhook/đối soát → request Succeeded, booking Deposited, một Payment, PaidAtUtc. Refresh payment-result hiển thị kết quả từ BE; không sửa DB thủ công để ép trạng thái. |
| 6 | Customer + Manager | Bổ sung người ở cùng, Manager duyệt CCCD và đề xuất danh sách nếu có. Chưa duyệt đủ thì không lập hợp đồng. |
| 7 | Manager | Tạo hợp đồng; DB có snapshot theo số người, application Converted, Customer không sửa hồ sơ này nữa. |
| 8 | Customer/Manager | Xem signed ảnh theo quyền; URL hết hạn, đổi tài khoản không còn preview cũ. Snapshot không đổi khi nguồn dữ liệu khác thay đổi. |
| 9 | Người vận hành | Ghi mã booking/request/payment/contract và thời điểm kiểm tra, không ghi CCCD/key/URL ký. Đối chiếu số tiền trên payOS với DB. |

Nếu upload 502: kiểm tra bucket có tồn tại và Private, đúng tên biến BE, key/project, giới hạn MIME/size. Nếu 409: tải lại hồ sơ; không gửi lại Version cũ. Nếu 401/403: kiểm tra đăng nhập/phạm vi quyền. Nếu không mở được picker trước khi có request: kiểm tra consent, trạng thái khóa hồ sơ, bản FE đã deploy và thử hard refresh sau deploy.

## Bằng chứng đã kiểm tra trong phiên này

- Public Railway: `/api/health-checks` 200, `/api/health-checks/database` 200; review chưa đăng nhập 401; Swagger có upload CCCD và snapshot hợp đồng.
- BE suites liên quan: 69/69 pass (bao gồm bài tích hợp toàn luồng mới).
- FE bộ test:booking-identity: 85/85 pass; TypeScript và Vite build pass.
- Script preflight: 3/3 test offline pass, 4/4 check public Railway pass; private bucket chưa được kiểm tra bằng key.
- Chưa thực hiện đăng nhập Customer trên production, chưa tạo bucket/đổi Variables/deploy, chưa upload ảnh thật, chưa chạy SQL trên Supabase và chưa thanh toán tiền thật. C12 UAT thật chưa được đánh dấu hoàn thành.
