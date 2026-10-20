# Phase 3.1B — Mười quyết định Billing còn chờ

Các quyết định định giá theo Property/Service, sáu chế độ, ngày hiệu lực đầu tháng kế tiếp và snapshot tại phát hành đã được duyệt. Các quyết định dưới đây chưa được suy diễn từ schema hiện tại.

| # | Quyết định / bằng chứng | Mặc định an toàn đề xuất | Gate bị ảnh hưởng |
|---|---|---|---|
| 1 | Nguồn quantity người và xe: audit chưa thấy nguồn chuẩn hóa đáng tin cậy cho occupants/vehicles. | Chưa tự tính; yêu cầu dữ liệu xác nhận có audit trail. Đánh giá ContractOccupants/ContractVehicles/ServiceSubscriptions trước tự động hóa. | Billing Engine; có thể cần schema quantity/subscription |
| 2 | RoomTypeServices là liên kết áp dụng hay mandatory? Rate của Property không tự xác định dịch vụ bắt buộc, opt-in hay tiêu thụ. | Không tự lập hóa đơn mọi rate; lưu applicability/subscription được owner xác nhận trước khi charge. | Billing Engine và public livingCosts |
| 3 | UtilityReadings hiện có start/end; thiếu số đọc, số âm, rollover, đổi người thuê và trùng kỳ cần xử lý ra sao? | Fail closed cho kỳ không hợp lệ; không tính từ Services.Price hay dữ liệu mẫu. | Validation/operations Billing |
| 4 | Giá lần đầu của Property có được hiệu lực tháng hiện tại hay vẫn đợi tháng sau? Đã duyệt đổi giá chỉ từ đầu tháng kế. | Rate mới không hồi tố; mặc định áp từ đầu tháng kế tiếp, bao gồm thiết lập lần đầu cho đến khi có policy ngoại lệ được duyệt. | Rate management/cutover |
| 5 | Hợp đồng bắt đầu/kết thúc giữa tháng; quantity người/xe thay đổi giữa kỳ. | Chưa tính partial month; cần chọn full-month/proration và quy tắc ngày trước khi triển khai. | Billing calculation |
| 6 | VND quantity precision, làm tròn dòng/tổng, thuế/discount và debit/credit âm. | Giữ Quantity numeric(18,4), Price/Amount numeric(18,2); chưa áp công thức hoặc CHECK cấm dòng điều chỉnh hợp lệ. | Billing Engine và kế toán |
| 7 | Draft/Issued/Paid; khi nào snapshot bất biến; sửa hóa đơn đã phát hành; idempotency và concurrency theo ContractId/InvoiceMonth. | Chụp giá lúc phát hành; hóa đơn issued bất biến, điều chỉnh bằng chứng từ mới; chốt unique/idempotency trước chạy đồng thời. | Billing lifecycle/schema |
| 8 | Live có 5 invoice và 25 lines không snapshot. | Giữ nguyên; đánh dấu legacy không thể tái dựng chắc chắn. Không backfill từ Amount/Description/Service.Name/frontend. Thiết kế đối soát payment riêng. | Data migration/audit |
| 9 | Services hiện có 4 mục; catalog chưa có Parking/phí người được xác nhận. | Owner duyệt ServiceId/mục đích trước nhập; không tạo catalog row hoặc giá mẫu trong 3.1B. Unit không suy từ tên tiếng Anh. | Catalog/data entry |
| 10 | Services.Price còn được đọc bởi ServiceCatalogService, RoomTypeRepository/RoomTypeServices và public room detail. | Giữ cột legacy; lập inventory consumer và chuyển public livingCosts/Billing sang PropertyServiceRates; thiếu rate thì không fallback, thể hiện rõ chưa cấu hình. | Phase 3.1C và Billing |

## Ranh giới dữ liệu

PropertyServiceRates xác định mức giá theo thời gian, không xác định có phát sinh charge hay số lượng. InvoiceDetails có cấu trúc snapshot, nhưng chưa có logic tạo hoặc phát hành hóa đơn. Không có câu trả lời nào trong bảng trên được “giải quyết” bởi việc thêm schema.