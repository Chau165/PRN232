# Đồng bộ FE P11/P12 sau merge main — 13/11/2026

Đã tích hợp phần FE đặt cọc payOS, yêu cầu hoàn cọc Customer, xét duyệt Admin, theo dõi Customer/Manager và báo cáo thu/hoàn cọc từ bản làm việc trước vào Project_ChuanBiDiThiBanMoi.

Giữ lazy loading/Suspense và phân quyền router từ main. Route /management/refunds chỉ Admin; Manager theo dõi tại quản lý booking. Route /management/reports vẫn Admin-only theo main hiện tại; BE báo cáo vẫn hỗ trợ scope Manager hiện tại. Không mở rộng quyền báo cáo trong lần đồng bộ này.

/payment-success chuyển tiếp tới /payment-result và xác minh trạng thái BE thay vì hiện thành công cố định. DepositPage gọi API tạo link booking, không gọi payBooking giả lập. Dữ liệu tài khoản nhận chỉ gửi khi Customer tạo đơn, không lưu localStorage/sessionStorage.

Không sửa BE/appsettings.json (thay đổi cấu hình có sẵn của người dùng), không chạy migration, không thay dependency/lockfile, không commit/push hay gọi payOS thật.

Kiểm chứng:

- Typecheck và production build: pass.
- FE Vitest: 41 files, 306/306 tests pass.
- Node tests ngày hợp đồng: 2/2 pass.
- BE BookingRefundTrackingApiTests + DepositReportApiTests: 17/17 pass, DB test cô lập.

Trước khi test môi trường thật, kiểm tra DB đích đã áp dụng AddBookingRefundRequestsAndPayoutAttempts và AddBookingRefundLedger; bật cấu hình kênh thu/chi theo tài liệu P11/P12. Thử tài khoản thật vẫn cần xác nhận môi trường/booking và khoản chi cụ thể.

- Test liên thông FE → BE → SDK merchant giả lập → webhook/phục hồi/tiền mặt → DB test → báo cáo: 3/3 pass. Không gọi payOS thật.
