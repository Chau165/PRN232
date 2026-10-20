# Phase 2E — Quyết định owner và release gates

| Gate | Trạng thái | Cách xử lý hiện tại / thông tin cần owner xác nhận |
|---|---|---|
| A. Public `RoomCode` | **PENDING OWNER APPROVAL** | Giữ contract Phase 2C/2D; dùng `RoomId` để navigation, không parse code. Owner xác nhận RoomCode có thể hiển thị anonymous không. |
| B. Property zero rentable Room | **PENDING OWNER APPROVAL** | Giữ `404` generic ở detail/room list. Owner chọn 404 hay 200 detail với zero count. |
| C. Ảnh | **NOT VERIFIED / BLOCKED** | Giữ `primaryImageUrl: null`, `images: []`; HTTPS/count không chứng minh host trust/anonymous ACL. Cần bucket/source, host allow-list, ACL test và xác nhận không dùng maintenance signed URLs. |
| D. Thiết bị | **PENDING OWNER APPROVAL** | Giữ `equipments: []`; cần public visibility và status vocabulary có nghĩa an toàn. Source hiện chấp nhận status bất kỳ và có supplier/warranty/maintenance data. |
| E. Automatic Property publication | **APPROVED cho MVP** | Property public khi có ít nhất một Room rentable. Không thêm `IsPublished`. |
| Legacy anonymous GET | **BLOCKED FOR PRODUCTION** | `GET /api/properties`, `/api/rooms` và route con vẫn lộ owner/status/image/amenity ngoài public allow-list. FE hiện không gọi; external/deployed caller UNKNOWN; Manager owner scope chưa có ở GET. Cần consumer inventory, owner/Admin matrix và migration approval. |
| Public reference/diagnostic GET | **PENDING APPROVAL** | RoomType/Service/Amenity/Role/Health GET vẫn anonymous. Xác nhận route nào là danh mục công khai; không giả định consumer ngoài repo vắng mặt. |
| `POST /api/roles` | **BLOCKED — SECURITY REVIEW REQUIRED** | Source không có `[Authorize]` cho action tạo Role. Cần owner quyết định Admin-only policy, kiểm kê caller, thêm positive/negative auth tests. Chưa sửa trong Phase 2E. |
| Booking write/concurrency | **OUT OF SCOPE / NOT READY** | Không có Booking POST/transaction chống double booking. Public availability chỉ là snapshot và không giữ phòng. |

## Trạng thái phát hành

**COMPLETE — PHASE 3 FE DEVELOPMENT READY; PRODUCTION BLOCKED**

Bốn read contracts đã kiểm chứng qua source/API tests và PostgreSQL HTTP test. Phase 3 có thể tích hợp theo contract hiện tại và phải xử lý placeholder/404 như tài liệu; điều này không đóng legacy security, owner approval hoặc booking write gates.
