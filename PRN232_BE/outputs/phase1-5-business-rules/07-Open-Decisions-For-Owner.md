# 07 — Các quyết định còn mở cho Project Owner
Ngày lập/đối chiếu: 2026-10-19 (Asia/Saigon). Snapshot live Supabase: 2026-10-18 17:00:33 UTC (transaction READ ONLY, ROLLBACK).


Tài liệu này tách bằng chứng hiện tại khỏi đề xuất. Các gợi ý bên dưới chưa phải quyết định được duyệt. Phần được owner trả lời có thể được dùng làm đầu vào Phase 2.

## D1 — Property nào được phép xuất bản? — P0, BLOCKING

1. **Câu hỏi:** Mọi Property có Room đủ điều kiện được public, hay cần trạng thái Published/Approved?
2. **Bằng chứng:** Properties không có publication/status field; PropertiesController.GetAll/GetById anonymous và không lọc; tạo Property chỉ Admin/Manager nhưng không có approval flow.
3. **Behavior hiện tại:** GET trả mọi Property; sự tồn tại/Owner không chứng minh ý chí quảng cáo.
4. **Option A:** Owner xác nhận mọi record đủ điều kiện đều public. Dễ làm, không cần schema; rủi ro lộ Property chưa được duyệt.
5. **Option B:** Chỉ Property explicitly Published/Approved. Kiểm soát và audit rõ; cần field/workflow.
6. **Option C:** Allow-list ngoài có quản lý. Tránh migration ngay nhưng cần nguồn allow-list và vận hành an toàn.
7. **Khuyến nghị:** B cho MVP bền vững; nếu demo không schema change thì chỉ A khi owner xác nhận rõ phạm vi dữ liệu.
8. **Phase 2:** Chặn listing/detail ẩn danh tới khi scope được biểu diễn/duyệt.
9. **DB change:** Có với B; không với A; C cần nguồn dữ liệu kiểm soát.
10. **Phối hợp:** Property module owner/Admin-Manager workflow; không sửa ở Phase 1.5.

## D2 — Booking statuses nào giữ phòng? — P0, BLOCKING

1. **Câu hỏi:** Trong Pending, Deposited, Cancelled, status nào chặn public availability?
2. **Bằng chứng:** Đây là 3 giá trị live; không có constants/controller/service; 1 Pending còn hạn, 3 Deposited và 1 Cancelled hết hạn.
3. **Behavior hiện tại:** Không có query/transition Booking; status không mang nghĩa đã xác minh.
4. **Option A:** Pending và Deposited giữ khi còn hạn; Cancelled không giữ.
5. **Option B:** Chỉ Deposited giữ; Pending chưa chấp nhận tiền thì không giữ.
6. **Option C:** Booking không chặn, chỉ Contract/Room.Status quyết định; nguy cơ phòng pending bị đặt trùng.
7. **Khuyến nghị:** A là safe candidate, nhưng yêu cầu owner xác nhận chính xác; không gọi Deposited là “Confirmed”.
8. **Phase 2:** Danh sách status allow-list đưa vào một Rentability predicate/test matrix; không tự dùng mọi status.
9. **DB change:** Không cần cho read query; cần xem xét khi có workflow/concurrency enforcement.
10. **Phối hợp:** Booking module owner. Source checkout hiện chưa có Booking lifecycle để sửa.

## D3 — ExpiryDate giải phóng thế nào? — P0, BLOCKING

1. **Câu hỏi:** Booking hold hết hạn đúng tại thời điểm nào và có cần tự đổi status không?
2. **Bằng chứng:** ExpiryDate là timestamptz NOT NULL; source không so sánh, job hoặc status update; live có 4 Booking quá hạn nhưng status chưa đổi.
3. **Behavior hiện tại:** Hết hạn không tự xóa row/đổi status trong code đã kiểm tra.
4. **Option A:** Tính động theo UTC; hold khi ExpiryDate > asOfUtc, equality đã hết hạn (<= release).
5. **Option B:** Status tiếp tục block tới khi staff đổi thủ công/worker đóng; dễ bị stale record giữ phòng.
6. **Khuyến nghị:** A cho public availability; nếu cần status quản trị tự sạch thì thiết kế worker riêng.
7. **Phase 2:** Dùng một instant asOfUtc xuyên suốt listing; kiểm thử trước/bằng/sau expiry.
8. **DB change:** Không cho query động; worker có thể không cần schema, tùy module.
9. **Phối hợp:** Booking owner quyết định lifecycle/notification nếu tự đóng.
10. **Owner confirmation:** Bắt buộc, gồm timezone UTC và equality boundary.

## D4 — Contract nào đang block? — P0, BLOCKING

1. **Câu hỏi:** Status Active và StartDate/EndDate được ưu tiên thế nào khi tính rentability?
2. **Bằng chứng:** Status live chỉ thấy Active (3); cột text không CHECK. Maintenance dùng UTC date và interval inclusive nhưng không kiểm tra Contract.Status; không có lifecycle.
3. **Behavior hiện tại:** Không có rentability code; Contract Active không tự sync Room.
4. **Option A:** Status Active và date interval bao gồm UTC ngày hiện tại thì block; maintenance semantics có precedent.
5. **Option B:** Contract Active với EndDate >= UTC date block, gồm cả start trong tương lai vì API không có requested move-in.
6. **Option C:** Mọi Contract Active block đến khi được staff đóng, bất kể EndDate; đơn giản nhưng Active cũ có thể block mãi.
7. **Khuyến nghị:** B cho listing generic “có thể nhận thuê” không nhận ngày chuyển vào; lấy ngày làm hiệu lực khi EndDate qua, đồng thời báo dữ liệu Active cũ. Đây vẫn là PROPOSED.
8. **Phase 2:** Chốt UTC date, Start/End inclusive, status/date conflict, cách xử lý StartDate > EndDate.
9. **DB change:** Không cho query; overlap enforcement/termination audit có thể cần schema/rule khác.
10. **Phối hợp:** Contract/Rooms module owners; không có lifecycle module trong source hiện tại.

## D5 — Hợp đồng tương lai, chồng lấn, kết thúc sớm — P0/P1

1. **Câu hỏi:** Contract Active bắt đầu trong tương lai có giữ phòng; dữ liệu overlap xử lý ra sao; cách ghi nhận chấm dứt sớm là gì?
2. **Bằng chứng:** Booking không có move-in date; Contract có date range nhưng không có termination fields; schema không có overlap constraint. Snapshot có 0 overlap nhưng đây không phải enforcement.
3. **Behavior hiện tại:** Không thấy validator/ngăn hai Contract cùng Room; không thấy auto-end/terminate flow.
4. **Option A:** Generic public listing loại mọi Active Contract có EndDate hôm nay hoặc sau; không hứa future-date availability.
5. **Option B:** Chỉ loại Contract hiện nằm trong khoảng ngày; phải có requested rental period/term để biết overlap.
6. **Khuyến nghị:** A cho MVP hiện tại; B chỉ khi API/FE bổ sung kỳ thuê và business rule overlap.
7. **Phase 2:** Đếm một Room một lần dù nhiều blocker; integrity conflict fail-closed/log nội bộ.
8. **DB change:** Không cho read query; future availability cần Booking start/end data. Enforce concurrent Contract overlap có thể cần transaction/constraint.
9. **Phối hợp:** Contract owner và nhóm Booking/lease workflow.
10. **Priority:** Future commitment P0 cho listing không có requested date; overlap/termination P1 nhưng phải chốt trước khi có lifecycle writes.

## D6 — Reserved và nguồn sự thật status — P0, BLOCKING

1. **Câu hỏi:** Reserved là trạng thái chính thức hay dữ liệu legacy; Booking/Contract hay Room.Status chịu trách nhiệm đổi trạng thái?
2. **Bằng chứng:** RoomStatuses.All có Available/Occupied/Maintenance; live có 1 Reserved; DB text không CHECK. Reserved hiện liên kết một Pending còn hạn.
3. **Behavior hiện tại:** Room update từ chối Reserved; RoomService cho đổi giữa ba status được code chấp nhận mà không kiểm tra blocker.
4. **Option A:** Reserved là state chính thức; bổ sung constants/validation/transition rule.
5. **Option B:** Reserved là legacy; dọn dữ liệu bằng change được owner duyệt.
6. **Khuyến nghị:** Tạm loại mọi status khác Available khỏi browse; chưa sửa record Reserved.
7. **Phase 2:** Chọn nguồn sự thật; không để Room Available lấn át Booking/Contract.
8. **DB change:** Không cần lưu Reserved; CHECK/data cleanup chỉ sau quyết định.
9. **Phối hợp:** Rooms module owner và Booking owner.
10. **Priority:** P0 vì status có thật nhưng CRUD không chấp nhận.

## D7 — Property public nhưng hết phòng/thiếu ảnh? — P1

1. **Câu hỏi:** Listing mặc định ẩn Property count=0? Detail có thể hiện Property đã publish nhưng hết phòng? Có bắt buộc ảnh/mô tả/nội quy?
2. **Bằng chứng:** User request duyệt count=0 và price=null; mặc định loại zero availability mới là recommendation. Schema không có Property image/Description/Rules.
3. **Behavior hiện tại:** Không có biểu diễn publication hoặc Property media.
4. **Option A:** Listing chỉ có count>0; detail của published Property vẫn có thể trả 0/null; ảnh null có placeholder.
5. **Option B:** Ẩn mọi Property hết phòng khỏi list và detail; yêu cầu media completeness riêng.
6. **Khuyến nghị:** A; không bịa nội dung và cho phép ảnh null.
7. **Phase 2:** UI có trạng thái hết phòng/placeholder; không gắn Property image giả.
8. **DB change:** Không cho A; có nếu thêm media/description/rules.
9. **Phối hợp:** Property content/UI owner.
10. **Priority:** P1; count/price calculation đã APPROVED, cách browse zero-room vẫn cần xác nhận.

## D8 — Public filter cùng TypeName — P1, NONBLOCKING CÓ ĐIỀU KIỆN

1. **Câu hỏi:** Tên trùng được gộp theo quy tắc chuẩn hóa nào?
2. **Bằng chứng:** Option 1 được owner duyệt; DB chỉ unique RoomTypeId, cho phép trùng TypeName; RoomTypeService Trim, chỉ kiểm tra tên rỗng/giá âm. Live hiện có 3 RoomTypes nhưng 0 nhóm tên trùng theo trim/collapse-space/case-insensitive. FE có label, chưa gửi TypeName hoặc RoomTypeId.
3. **Behavior hiện tại:** API management trả từng ID; RoomTypeServices/giá gắn đúng ID.
4. **Option A:** Public filter roomTypeName khớp mọi ID theo trim + collapse whitespace + không phân biệt hoa thường.
5. **Option B:** Chỉ filter bằng RoomTypeId; không đáp ứng yêu cầu user thấy một “Studio” gồm nhiều mức giá.
6. **Khuyến nghị:** A cho public search; ID chỉ là identity/admin filter. Chuẩn hóa nâng cao/alias cần convention sau.
7. **Phase 2:** Dropdown hiển thị một logical label nhưng mỗi Room giữ RoomTypeId/price/service thật.
8. **DB change:** Không bắt buộc; nếu cần stable category/alias chính thức thì có thể cần taxonomy/schema riêng.
9. **Phối hợp:** RoomType/FE owner để tránh typo và gán nhầm ID.
10. **Priority:** P1; không chặn rentability core sau khi naming rule được ghi nhận.

## D9 — Legacy anonymous API và Owner fields — P0 security review

1. **Câu hỏi:** OwnerId/OwnerName trong anonymous GET /api/properties có được phép public không; có harden route cũ không?
2. **Bằng chứng:** PropertiesController GET không authorize; PropertyDto/Repository trả OwnerId/OwnerName. GET rooms/images/amenities cũng không kiểm tra public visibility/rentability.
3. **Behavior hiện tại:** Dữ liệu hiện có thể được anonymous API trả.
4. **Option A:** Giữ exposure có chủ ý, owner xác nhận từng field.
5. **Option B:** Route cũ chỉ dành cho management/auth hoặc DTO được thu gọn trong remediation riêng.
6. **Khuyến nghị:** B nếu Owner fields không phải thông tin public; public API mới dùng allow-list DTO bất kể lựa chọn route cũ.
7. **Phase 2:** Route mới không sửa exposure legacy; hardening route cũ cần kiểm tra consumer để tránh break compatibility.
8. **DB change:** Không cần; API/auth/DTO change có thể cần.
9. **Phối hợp:** API/security maintainer và FE consumer owner.
10. **Priority:** P0 cần triage trước khi mở rộng browsing; Phase 1.5 không thay đổi route.

## D10 — Phí Service billing unit — P2, NONBLOCKING

Services chỉ có Name/Price và RoomTypeServices gắn theo RoomTypeId. Không có billing unit/optional flag. Đề xuất chỉ hiển thị tên + giá với nhãn chưa xác định chu kỳ hoặc ẩn phí; không cộng thành tổng monthly charge. Muốn tổng phí thì owner cần duyệt semantics và có thể cần schema/module change.

## Trả lời owner để mở gate

Có thể trả lời theo mã, ví dụ:

- D1: B; người/role được quyền publish: ...
- D2: A; hold statuses: ...
- D3: A; expiry equality: ...
- D4: B; Active + date precedence: ...
- D5: A; contract tương lai giữ tới ngày nào: ...
- D6: Reserved là ...
- D7: A; zero-room detail và ảnh null: ...
- D8: A; normalization: ...
- D9: A/B; OwnerName/OwnerId public: ...


