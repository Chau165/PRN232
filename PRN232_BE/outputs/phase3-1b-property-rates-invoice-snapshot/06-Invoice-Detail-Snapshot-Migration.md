# Phase 3.1B — Migration 3: InvoiceDetails snapshot

**Migration:** 20261019143633_AddInvoiceDetailBillingSnapshot  
**Bảng hiện có:** InvoiceDetails; ItemType, Description và Amount được giữ nguyên.

## Cột mới nullable

- ServiceId integer? → FK Services, ON DELETE RESTRICT.
- RateId integer? → cùng ServiceId tham chiếu khóa (RateId, ServiceId) của PropertyServiceRates, ON DELETE RESTRICT.
- Quantity numeric(18,4)? để giữ lượng tiêu thụ phân số.
- UnitPrice numeric(18,2)? theo VND và schema tiền hiện tại.
- UnitCode text? và BillingBasis text? lưu snapshot literal tại thời điểm phát hành.

CHECK yêu cầu RateId chỉ được điền khi ServiceId có giá trị. Cặp FK bảo đảm RateId đúng với ServiceId. Các cột nullable cho phép dòng tiền thuê/điều chỉnh và toàn bộ dữ liệu legacy tiếp tục hợp lệ. Amount vẫn là số tiền đã ghi; không được tính lại từ giá mới.

## Bảo toàn hóa đơn cũ

Live có 25 InvoiceDetails. Migration chỉ ADD COLUMN/index/constraint; không backfill và không sửa Amount, Description hoặc ItemType. Test fixture chèn 25 dòng legacy trước migration và xác nhận 25 dòng, Amount/Description không đổi, mọi cột snapshot vẫn NULL sau nâng cấp. Test thêm dòng mới có Quantity phân số và snapshot đủ trường; khi rate được đóng thời hạn/thay bằng rate mới, snapshot dòng cũ không bị thay đổi. FK ngăn xóa Service/Rate còn được tham chiếu.

Down có guard từ chối nếu bất cứ trường snapshot nào đã có dữ liệu, vì xóa cột sẽ mất chứng cứ hóa đơn. Thực tế rollback sau khi snapshot được dùng cần forward fix hoặc rollback ứng dụng tương thích, không chạy Down mù.

## Phạm vi chưa làm

Không có phép nhân Quantity × UnitPrice, làm tròn, thuế/discount, proration, Draft/Issued/Paid transition, Invoice POST hay payment mutation. Billing Engine cần quyết định tại 07-Billing-Remaining-Decisions.md. Dòng legacy chưa thể phục dựng snapshot đáng tin cậy.

SQL review: SQL_REVIEW/03-AddInvoiceDetailBillingSnapshot.sql. Không thực thi trên Supabase.