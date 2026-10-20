# 06 — Live postflight, integrity và RLS

**Postflight sau migration: NOT RUN — không gate nào được áp dụng.**

Preflight trước mọi thay đổi ghi nhận: Properties 3, InvoiceDetails 25, MaintenanceLogs 9, MaintenanceReports 4, MaintenanceReportImages 5; các bảng/cột Phase 3.1B đích chưa có. RLS bật, 0 policy trên các bảng được preflight.

Task không ghi live nên không có post-migration delta. Không khẳng định không có thay đổi đồng thời từ ứng dụng/người vận hành khác. Không lấy fingerprint/giá trị Booking, Contract, Invoice hoặc Maintenance; task không sửa các bảng này.
