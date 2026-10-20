# 05 — Đặc tả Room Rentability cuối cùng (có điều kiện)

Ngày lập/đối chiếu: 2026-10-19 (Asia/Saigon). Snapshot live Supabase: 2026-10-18 17:00:33 UTC (transaction READ ONLY, ROLLBACK). Đây là đặc tả Phase 1.5, chưa triển khai. Không có predicate cuối nào được coi là APPROVED ngoài các phép tính Home được người dùng duyệt.

## Phân loại rule

- **APPROVED:** một Home card đại diện một Property; AvailableRoomCount đếm Room duy nhất thực sự đủ điều kiện thuê; StartingPrice là MIN(RoomTypes.Price) trên đúng cùng tập Room; nếu count=0 thì price=null; giữ nguyên giá theo RoomTypeId (Option 1).
- **CONFIRMED:** source Room CRUD chỉ chấp nhận Available/Occupied/Maintenance; live còn có Reserved; Bookings.Status hiện có Pending/Deposited/Cancelled; Contracts.Status hiện có Active; source không có Booking/Contract lifecycle.
- **BLOCKED:** chính xác Booking statuses nào giữ chỗ, ExpiryDate có giải phóng hay không, Contract effective/future semantics, Reserved và Property publication.
- **PROPOSED:** quy tắc bên dưới là gợi ý an toàn, không phải phê duyệt của owner.

## Mô tả nghiệp vụ

Một Room chỉ được trả là có thể thuê khi:

1. Property được phép public theo chính sách đã phê duyệt.
2. Room.Status chính xác là Available.
3. Không có Booking thuộc một status đã được owner xác nhận là hold và chưa hết hạn theo instant UTC.
4. Không có Contract được owner xác nhận là đang chiếm hoặc đã cam kết cho tương lai.
5. Dữ liệu liên quan không ở trạng thái lạ/mâu thuẫn cần xử lý.

Điều kiện 2–5 vẫn cần quyết định cho Public Browsing; do đó kết quả hiện tại là **BLOCKED**, không được trả số lượng “rentable” như một dữ kiện đã xác nhận.

## Predicate đề xuất có điều kiện

IsRoomRentableNow(room, asOfUtc):

~~~text
property is approved for public publication
AND room.Status == "Available"
AND no booking exists for this Room such that:
    booking.Status is in the owner-approved hold-status set
    AND booking.ExpiryDate > asOfUtc
AND no contract exists for this Room such that:
    contract.Status is an owner-approved blocking status
    AND contract has a valid date interval
    AND contract blocks a generic current rental offer
AND there is no unrecognized or contradictory availability state
~~~

Đề xuất cấu hình cụ thể để owner cân nhắc:

- Booking hold set: Pending và Deposited; Cancelled không giữ.
- Booking chỉ giữ trước expiry; ExpiryDate <= asOfUtc được coi là đã hết hạn. ExpiryDate là timestamptz NOT NULL, nên schema đủ cho kiểm tra instant động.
- Contract: status Active là candidate blocking status. Với API không nhận ngày bắt đầu/thời hạn thuê, khuyến nghị chặn cả Contract đã bắt đầu lẫn Contract Active trong tương lai có EndDate >= asOfUtcDate. Điều này bảo thủ hơn “đang có người thuê ngay bây giờ” và tránh quảng bá một cam kết tương lai. Maintenance hiện chỉ xác nhận khoảng ngày hiện tại inclusive, không xác nhận quy tắc này cho thuê.
- Active Contract có EndDate trước ngày UTC hiện tại được coi là hết hiệu lực theo ngày trong đề xuất, nhưng Status còn Active là trạng thái dữ liệu cần theo dõi; không có worker tự đóng.
- Status không nhận diện, date range đảo ngược, hoặc Booking/Contract dữ liệu mâu thuẫn: fail closed cho Room đến khi owner/module xác nhận.

Không thể đổi các candidate ở trên thành quy tắc APPROVED trong Phase 1.5.

## LINQ design đề xuất

Đây là phác thảo để Phase 2 hiệu chỉnh sau khi quyết định được duyệt; không phải mã đã chạy/compile:

~~~csharp
var asOfUtc = timeProvider.GetUtcNow().UtcDateTime;
var asOfUtcDate = DateOnly.FromDateTime(asOfUtc);
var approvedHoldStatuses = ownerApprovedHoldStatuses;

var rentableRooms = db.Rooms
    .AsNoTracking()
    .Where(r => r.Status == RoomStatuses.Available)
    .Where(r => propertyPublicationPolicyAllows(r.Property))
    .Where(r => !r.Bookings.Any(b => !recognizedBookingStatuses.Contains(b.Status)))
    .Where(r => !r.Contracts.Any(c => !recognizedContractStatuses.Contains(c.Status)))
    .Where(r => !r.Bookings.Any(b =>
        approvedHoldStatuses.Contains(b.Status) &&
        b.ExpiryDate > asOfUtc))
    .Where(r => !r.Contracts.Any(c =>
        c.Status == ownerApprovedBlockingContractStatus &&
        (c.EndDate >= asOfUtcDate || c.StartDate > c.EndDate)));
~~~

propertyPublicationPolicyAllows, published_property_ids, hold set, recognized status sets và Contract rule là placeholders BLOCKED, chưa có trong source. published_property_ids là allow-list tham số ngoài schema hiện tại; nếu chọn publication field thì thay bằng cột mới sau migration. Nên tính asOfUtc/asOfUtcDate một lần cho cả listing/count/page. Mọi room-level filter phải áp lên chính Room trong query này.

## PostgreSQL SELECT design đề xuất

Không chạy query này trong Phase 1.5; các tham số và hold statuses chỉ là chỗ cắm sau khi owner duyệt.

~~~sql
WITH rentable_rooms AS (
    SELECT r."RoomId", r."PropertyId", r."RoomTypeId", rt."Price"
    FROM public."Rooms" r
    JOIN public."RoomTypes" rt ON rt."RoomTypeId" = r."RoomTypeId"
    JOIN public."Properties" p ON p."PropertyId" = r."PropertyId"
    WHERE r."Status" = 'Available'
      AND p."PropertyId" = ANY (@published_property_ids)
      AND NOT EXISTS (
          SELECT 1 FROM public."Bookings" b
          WHERE b."RoomId" = r."RoomId"
            AND NOT (b."Status" = ANY (@recognized_booking_statuses))
      )
      AND NOT EXISTS (
          SELECT 1 FROM public."Contracts" c
          WHERE c."RoomId" = r."RoomId"
            AND NOT (c."Status" = ANY (@recognized_contract_statuses))
      )
      AND NOT EXISTS (
          SELECT 1
          FROM public."Bookings" b
          WHERE b."RoomId" = r."RoomId"
            AND b."Status" = ANY (@approved_hold_statuses)
            AND b."ExpiryDate" > @as_of_utc
      )
      AND NOT EXISTS (
          SELECT 1
          FROM public."Contracts" c
          WHERE c."RoomId" = r."RoomId"
            AND c."Status" = @approved_blocking_contract_status
            AND (c."EndDate" >= @as_of_utc_date OR c."StartDate" > c."EndDate")
      )
)
SELECT "PropertyId",
       COUNT(DISTINCT "RoomId") AS "AvailableRoomCount",
       MIN("Price") AS "StartingPrice"
FROM rentable_rooms
GROUP BY "PropertyId";
~~~

Phần SQL chỉ đúng nếu owner phê duyệt các status/date placeholders. Date check ở đây theo đề xuất bảo thủ bao gồm Contract tương lai. Nếu owner chọn “occupancy hiện tại” thì thay điều kiện bằng StartDate <= as_of_utc_date AND EndDate >= as_of_utc_date.

## Ví dụ kết quả

| Property/Room data | Rentable count | StartingPrice | Phân loại |
|---|---:|---:|---|
| Property đã public; Available, không Booking/Contract blocker, giá 3.0m | 1 | 3.0m | Ví dụ theo predicate sau khi được duyệt |
| Hai Room Available; một Pending còn hạn đã được xác nhận hold; giá 3.0m/4.0m | 1 | 4.0m | Booking hold là PROPOSED |
| Available + Deposited đã hết hạn, không Contract blocker | 1 | giá của Room | Booking expiry boundary là PROPOSED; status không tự đổi |
| Occupied + Active Contract hiện hiệu lực | 0 từ Room này | không lấy giá Room này | Live có 3 trường hợp tương tự |
| Reserved + Pending còn hạn | 0 từ Room này | không lấy giá Room này | Live có 1 trường hợp; Reserved fail closed |
| Available + Active Contract bắt đầu tháng tới | 0 theo đề xuất generic listing | không lấy giá Room này | Future commitment policy cần owner |
| Property có 0 Room rentable | 0 | null | APPROVED cho null; mặc định loại khỏi listing là PROPOSED |
| Room có Booking status lạ hoặc date range Contract đảo ngược | Không công bố Room là rentable | không đưa vào MIN | Fail-closed là PROPOSED an toàn |

AvailableRoomCount = COUNT(DISTINCT RoomId) và StartingPrice = MIN(RoomTypes.Price) trên cùng rentable set là **APPROVED** theo request. Giá luôn join qua Room.RoomTypeId; TypeName trùng không làm đổi giá. Kết quả theo filter/Property phải không nhân bản Room do join ảnh/amenities. Mặc định ẩn Property zero-availability là khuyến nghị trước đó, chưa phải owner approval độc lập.

## Performance, privacy và giới hạn

- Dùng EXISTS/NOT EXISTS trong truy vấn PostgreSQL, projection và aggregate server-side; không nạp toàn bộ Room/Booking/Contract vào memory.
- Bắt đầu từ Rooms có Status Available; Booking và Contract có index RoomId hiện có. Chưa có index ghép theo RoomId+status+expiry/date; chỉ đề xuất sau EXPLAIN trên query thật.
- Tránh N+1; lấy trang Property sau khi aggregate và sort ổn định bằng PropertyId. Room page tie-break bằng RoomId. Dùng EXISTS cho amenities để join không nhân count.
- Cần quyết định filter có chỉ chọn Property hay đếm/giá theo subset match. Theo định nghĩa đã duyệt, count và starting price nên mô tả toàn bộ rentable Rooms của Property; filter chỉ quyết định Property có match hay không, trừ khi API đặt tên riêng matchingRoomCount.
- Response chỉ có derived availability; không trả BookingId/CustomerId/DepositAmount, ContractId/CustomerId/Status/date/rent, Owner PII hoặc maintenance/private equipment fields.
- Không tuyên bố production query plan đã được đo. Không tạo index, migration hoặc source code trong audit này.



