# 04 — Quy tắc công khai Property

Ngày lập/đối chiếu: 2026-10-19 (Asia/Saigon). Snapshot live Supabase: 2026-10-18 17:00:33 UTC (transaction READ ONLY, ROLLBACK). Dữ liệu live chỉ đọc aggregate/catalog; không lộ Owner hay tenant identity.

## Kết luận

**BLOCKED** — Không có cột hoặc workflow biểu thị Property đã được duyệt/công khai. API GET hiện không xác minh publication; dữ liệu hiện có không đủ để quyết định mỗi Property nào được phép quảng bá.

## Schema và source thực tế

Live `public.Properties` có `PropertyId`, `OwnerId`, `PropertyName`, `AddressText`, `Ward`, `District`, `City`, `Latitude`, `Longitude`; không có `IsPublished`, `IsActive`, `Visibility`, `Status`, `ApprovalStatus`, Description, Rules hoặc Property image. Bảng live có 3 rows; số rows không biểu thị trạng thái duyệt.

Entity `PRN232.Domain/Entities/Property.cs:3–19` chỉ có các trường tương ứng. `PropertiesController.GetAll/GetById` ở `PRN232.Api/Controllers/PropertiesController.cs:21–31` không gắn `[Authorize]` và không lọc visibility; comment source gọi GET là browse công khai. `PropertyRepository.GetAllAsync` lọc tùy chọn `ownerId` nhưng nếu bỏ thì trả toàn bộ (`PRN232.Infrastructure/Repositories/PropertyRepository.cs:18–35`).

Tạo Property chỉ qua POST có `[Authorize(Roles="Admin,Manager")]`; `PropertyService.CreateAsync` dòng 20–48 gán OwnerId từ user đang đăng nhập, yêu cầu tên/địa chỉ không rỗng, không tạo trạng thái pending/approval. PUT cho Admin/Manager và kiểm tra ownership (`PropertiesController` dòng 49–67; `PropertyService` dòng 51–76). Không có duyệt Property, bật/tắt public, hoặc ẩn record trong source. Vậy không phải “bất kỳ ai cũng tạo được”, nhưng việc có Owner/được tạo không chứng minh đã được phép quảng cáo.

GET hiện trả `PropertyDto` có OwnerId/OwnerName và RoomCount tổng. Đây là exposure anonymous hiện hữu; route public mới cần DTO allow-list, và vấn đề route cũ phải được đánh giá như một remediation bảo mật riêng. Không sửa route trong Phase 1.5.

## So sánh publication options

| Option | Hợp schema hiện tại? | Ưu điểm | Rủi ro/chi phí | Đánh giá |
|---|---|---|---|---|
| A. Mọi Property có ít nhất một Room rentable đều public | Không thể xác định ý chí xuất bản; query khả dụng làm được sau khi chốt rentability | Không migration; ít bước cho demo | Owner có thể chưa muốn quảng cáo; “có dữ liệu” bị hiểu nhầm là đồng ý public | Chỉ dùng nếu Product Owner xác nhận rõ mọi record đủ điều kiện được public |
| B. Chỉ Property được explicitly Published/Approved | Chưa; thiếu trường và workflow | Có audit/control rõ; phù hợp public listing bền vững | Cần schema + Property management/API/UI hoặc allow-list ngoài có kiểm soát | **Khuyến nghị MVP** |
| C. Property “đủ điều kiện” theo completeness + có Room rentable | Một phần; bắt buộc tên/địa chỉ trong create, nhưng không có ảnh/description/rules/approval | Lọc record lỗi và phòng không còn | Không chứng minh chủ sở hữu muốn public; tiêu chuẩn “đầy đủ” chưa được định nghĩa | Có thể bổ sung vào B như validation, không thay thế publication consent |

Một Property được tạo bởi Admin/Manager và có Owner không đủ để tự suy ra public approval. RLS hiện bật trên bốn bảng được kiểm tra nhưng catalog không cho thấy policies; không dựa vào RLS để quyết định public scope. Enforce allow-list trong API phải là điều kiện riêng.

## Khuyến nghị MVP và quyết định cần owner

- Trước anonymous listing, chọn B: trạng thái publication rõ ràng do role được chỉ định bật/tắt; Phase 1.5 không tạo field hoặc thay đổi workflow.
- Nếu nhóm muốn chạy MVP không migration, owner phải phê duyệt rõ Option A và danh sách/điều kiện nào áp dụng cho dữ liệu đang có; không lấy kết quả GET hiện tại làm approval.
- Home mặc định chỉ nên liệt kê Property đã public và có ít nhất một Room rentable. Đây là **PROPOSED**. Với Property public nhưng hết phòng: đề xuất ẩn khỏi default listing; chi tiết có thể trả `availableRoomCount=0`, `startingPrice=null` nếu business muốn trang hết phòng.
- Không có ảnh Property riêng. Có thể trả ảnh Room rentable kèm `sourceRoomId`; nếu thiếu ảnh, trả null/placeholder. Không giả ảnh phòng là ảnh Property. Việc có bắt buộc ảnh, mô tả hoặc nội quy hay không cần owner quyết định.
- Nếu chọn publication field, cần migration và phối hợp Property module (Create/Update + quyền publish + DTO/UI). Không cần đổi RoomTypes pricing schema.

Mọi quyết định publication vẫn **BLOCKED**; không có field hiện tại để tự thực hiện Option B.

