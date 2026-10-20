# Phase 2A — Approved business rules and verified gaps

Ngày đối chiếu: 2026-10-19 (Asia/Saigon). Live Supabase được truy vấn trong transaction `READ ONLY` và rollback; snapshot UTC: `2026-10-18 17:36:31.735589`.

## Quyết định đã được duyệt trong prompt Phase 2A

| Chủ đề | Quy tắc được duyệt |
|---|---|
| Home card | Một card cho mỗi `Property`; đi từ Property đến danh sách Room thuê được rồi đến Room detail. |
| Room status | `Available` là điều kiện cần nhưng chưa đủ. `Reserved`, `Occupied`, `Maintenance` và status không nhận diện đều không thuê được. |
| Pending Booking | Chặn khi `ExpiryDate > asOfUtc`. Tại đúng expiry (`ExpiryDate == asOfUtc`) và sau expiry thì không còn chặn trong read-side query. |
| Deposited Booking | Chặn bất kể `ExpiryDate` cho tới khi có release/cancellation hợp lệ hoặc handoff được bảo vệ sang Contract. Không cho client tự đặt `Deposited`. |
| Cancelled Booking | Không tự chặn; blockers khác vẫn có hiệu lực. |
| Active Contract | Chặn bất kể ngày bắt đầu/kết thúc. Date range không thay thế lifecycle status. |
| Property visibility MVP | Không thêm cột publish. Listing tự hiển thị Property có ít nhất một Room đủ điều kiện thuê. |
| Giá | Giữ Option 1: giá theo `Rooms.RoomTypeId -> RoomTypes.Price`; không thêm `Rooms.MonthlyPrice`. TypeName trùng không gộp ID/giá/dịch vụ. |
| Count/StartingPrice | Cùng một rentable set: số Room duy nhất; MIN giá trên đúng set đó; count bằng 0 thì price null. |
| Public response | DTO allow-list riêng, không có Owner/customer/tenant/Booking/payment/Contract/identity hay dữ liệu quản trị nhạy cảm. |

## Source và live schema đã xác minh

- Backend hiện có `Available`, `Occupied`, `Maintenance`; `Reserved` có trong live DB nhưng trước Phase 2A không được `RoomService.UpdateAsync` chấp nhận.
- Live snapshot: Rooms `Available=4`, `Reserved=1`, `Occupied=3`, `Maintenance=1`; Bookings `Pending=1`, `Deposited=3`, `Cancelled=1`; Contracts `Active=3`.
- `Bookings.Status` và `Contracts.Status` là `text NOT NULL`; Booking `ExpiryDate` là `timestamp with time zone NOT NULL`; Contract `StartDate/EndDate` là `date NOT NULL`; Room status là `text NOT NULL`.
- Index `Bookings(RoomId)` và `Contracts(RoomId)` không unique. `Contracts(BookingId)` có unique index. Không có CHECK constraint trên Room/Booking/Contract status hoặc Contract date range.
- Chỉ Contract status `Active` được xác nhận trong source và live snapshot. Không có Booking/Contract create/update/deposit/cancel service/controller/repository hay expiry worker.

## Cách cài fail-closed cho status chưa biết

`Cancelled` là status Booking duy nhất đã được xác nhận không giữ phòng; Pending hết hạn được bỏ khỏi read-side blockers; Deposited luôn block. Booking status ngoài `{Pending, Deposited, Cancelled}` fail-closed.

Với Contract, chỉ `Active` được xác minh và không có status kết thúc/release được kiểm chứng. Vì vậy Phase 2A fail-closed với mọi Contract record: Active block theo quyết định; status khác cũng block cho tới khi có lifecycle vocabulary đã xác minh. Đây là cách xử lý status chưa biết, không phải kết luận rằng mọi Contract status tương lai vĩnh viễn phải chiếm phòng.

## Những điều Phase 2A chưa làm

- Không tạo Booking/Contract write API, không đặt expiry 30 phút khi tạo Booking, và không có cơ chế chống hai POST đồng thời.
- Không đổi Room/Booking/Contract live data; không tự giải phóng Room `Reserved` hoặc Booking hết hạn.
- Không tạo public browsing endpoint; availability query chỉ là read-side foundation cho Phase 2B.
- Không sửa anonymous legacy Property/Room routes. Chi tiết exposure và kế hoạch giảm rủi ro nằm trong `04-Phase2B-API-Contract-And-Security-Gate.md`.
