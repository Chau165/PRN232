# 08 — Readiness và kế hoạch thực hiện Phase 2

Ngày lập/đối chiếu: 2026-10-19 (Asia/Saigon). Snapshot live Supabase: 2026-10-18 17:00:33 UTC (transaction READ ONLY, ROLLBACK). Không có Phase 2 code, tests, migration hoặc DB change nào được thực hiện.

## Kết quả

**COMPLETE — BLOCKED PENDING OWNER DECISIONS**

Các artifact Phase 1 đã được đọc và đối chiếu lại. Các quyết định người dùng duyệt trong prompt được giữ nguyên. Booking/Contract/Public visibility/Reserved và exposure của anonymous legacy API vẫn có blockers. Không tuyên bố Phase 2 sẵn sàng public.

## Decision gates

| Gate | Kết quả | Bằng chứng và hành động mở gate |
|---|---|---|
| A — Room status | PASS WITH DOCUMENTED LIMITATIONS | Code chấp nhận Available/Occupied/Maintenance; live có Reserved; chỉ Available là điều kiện cần. Chốt Reserved ở F; không dùng Status đơn lẻ. |
| B — Booking hold status | BLOCKED | Live Pending/Deposited/Cancelled; không có lifecycle. Owner duyệt hold-status allow-list và Cancelled semantics (D2). |
| C — Booking expiration | BLOCKED | ExpiryDate timestamptz NOT NULL nhưng source không kiểm tra/tự expire; live còn 4 Booking quá hạn với status cũ. Duyệt UTC boundary và status job policy (D3). |
| D — Contract effective status | BLOCKED | Live chỉ thấy Active; không có Contract lifecycle/constants cho thuê. Duyệt status nào block (D4). |
| E — Contract dates | BLOCKED | Maintenance dùng UTC date inclusive, không đọc Status; future/expired/overlap rental semantics chưa duyệt. Duyệt D4–D5. |
| F — Reserved consistency | BLOCKED | 1 row live, Room update CRUD từ chối, text/no CHECK; Rooms owner chọn official hay legacy và sync (D6). |
| G — Property publication | BLOCKED | Schema không có publication field; GET anonymous trả mọi Property; không approval flow. Chọn A/B/C ở D1. |
| H — Pricing Option 1 | PASS WITH DOCUMENTED LIMITATIONS | User đã APPROVED giá theo RoomTypeId. Live không unique TypeName, CRUD không từ chối trùng; snapshot hiện không có duplicate group; RoomTypes không scoped theo Property, nên assignment là convention. |
| I — Same-name RoomType filter | PASS WITH DOCUMENTED LIMITATIONS | Có thể lọc tên logic qua mọi TypeId; current FE chỉ có nút label, chưa gửi ID/name; normalization chưa có contract. Duyệt convention D8 trước FE/API integration. |
| J — Data privacy | BLOCKED | Anonymous GET hiện trả OwnerId/OwnerName và room endpoints không lọc availability/publication. DTO allow-list cho route mới chưa triển khai; owner/security maintainer triage legacy route D9. |

## Có thể làm gì an toàn?

**Đã chốt, có thể mang nguyên trạng sang thiết kế Phase 2:** một card/Property; count distinct Room; min giá từ cùng tập Room đủ điều kiện; count 0 thì price null; price theo RoomTypeId; public logical type search không được nhầm TypeName với ID.

**Chưa an toàn để bật anonymous result:** không thể tạo giá/count “rentable” đáng tin cậy khi hold statuses, Contract dates và publication chưa duyệt. API skeleton/DTO allow-list có thể chuẩn bị sau nhưng không nên trả kết quả thật hoặc mở public trước khi gate B–G/J giải quyết. Không cần schema change cho query availability nếu owner chấp nhận các cột hiện có; explicit publication thường cần field + migration.

## Thứ tự khuyến nghị

1. **Owner decision checkpoint:** trả lời D1–D6 và D9. Ghi rõ status strings, UTC boundary, contract future policy, Reserved và publication role.
2. **P2A — Rentability query + tests:** implement một predicate server-side theo các quyết định; test matrix Booking trước/tại/sau expiry, Contract boundary/future/ended/overlap, Reserved/unknown status và cross-table mismatch. Dùng cùng asOfUtc cho count/page.
3. **Publication prerequisite:** nếu D1 chọn explicit approval, thêm schema/workflow trong migration riêng được duyệt trước khi bật routes.
4. **P2B — Property listing:** query một row/Property, aggregates đúng set, server-side filter/page/sort ổn định; không trả owner fields.
5. **P2C — Property detail + room list:** cùng predicate/DTO với list, không N+1, 404 nếu hidden.
6. **P2D — Public Room detail/catalog/media:** RoomImages, RoomAmenities, RoomTypeServices và equipment allow-list; service fee không cộng thành monthly total.
7. **P2E — Integration verification:** chạy với test database/fixture an toàn trước; smoke GET Supabase chỉ sau deployment được duyệt và không ghi data. Theo dõi query plan; thêm index chỉ khi đo chứng minh cần.
8. **Phase 3 — FE integration:** nối API sau khi response/search contract ổn định; bỏ các mock fields không có nguồn.

## Schema và phối hợp

| Hạng mục | Có schema change không? | Ghi chú |
|---|---|---|
| Rentability theo Status/ExpiryDate/Contract dates hiện có | Không bắt buộc cho read query | Chỉ sau khi owner chốt semantics |
| Property publication field (D1 Option B) | Có | Migration + Property API/management UI/publish authorization |
| Booking future move-in search | Có | Bookings hiện không có ngày bắt đầu/kết thúc thuê |
| Contract không EndDate / early termination audit | Có thể cần | EndDate hiện NOT NULL, không có termination field |
| Chặn concurrent Booking/Contract conflict | Có thể cần | Hiện chỉ có index RoomId thường, chưa có unique/exclusion guard; cần Booking/Contract workflow |
| Pricing Option 1, duplicate TypeName | Không | Schema/CRUD hiện cho phép; không thêm MonthlyPrice hay PropertyId |
| Service total monthly | Có thể cần | Thiếu billing unit/mandatory semantics |
| Geo radius chính xác | Có thể cần | Latitude/Longitude numeric(18,2), không phù hợp 1–10 km |

Phối hợp tối thiểu: Product Owner; Rooms module owner; Property publish owner/Admin-Manager workflow; Booking/Contract owners; API/security maintainer cho legacy anonymous fields; FE owner cho logical RoomType filter.

## Test strategy (kế hoạch, chưa chạy)

- Unit/service: predicate theo từng status và expiry boundary; ngày Contract inclusive theo UTC; Active future/expired; unknown/malformed states fail closed.
- Repository/PostgreSQL: NOT EXISTS không nhân Room; duplicate TypeName IDs vẫn giữ giá/service chính xác; amenities AND trên cùng Room; property count/min giá lấy cùng eligible set; paging + stable tie-break.
- Cross-table cases: Available + active Booking/Contract; Reserved + expired Booking; Occupied + không Contract; Room có nhiều Booking/overlapping Contracts; Property hidden.
- API/privacy: AllowAnonymous chỉ trên public routes, không trả Owner/tenant/booking/contract/payment/maintenance data; 404 không phân biệt room hidden/non-rentable; legacy route decision được kiểm thử.
- Regression: management CRUD, Auth, Maintenance, Notification.
- Live Supabase smoke test chỉ đọc; không tạo test Booking/Contract và không ghi business data.

## Rủi ro chính

1. Status là text không có CHECK: status mới có thể lọt vào mà code không hiểu.
2. NOT EXISTS read query không tự chống race khi tạo Booking/Contract.
3. Dữ liệu hiện tại nhất quán ở mẫu nhỏ (9 Rooms, 5 Bookings, 3 Contracts) nhưng không chứng minh mọi record tương lai hợp lệ.
4. Anonymous route hiện có lộ OwnerId/OwnerName; thêm route mới không tự sửa route cũ.
5. Contract date rule của Maintenance không phải approval cho rentability.
6. RLS bật nhưng không có policy quan sát được cho các bảng mục tiêu; không dùng RLS làm publication rule.
7. RoomTypes dùng chung toàn hệ thống; CRUD không kiểm tra RoomType thuộc Property.

## Khuyến nghị cuối

Chưa bắt đầu public browsing implementation. Bước tiếp theo là xin owner quyết định D1–D6 và D9; sau đó làm P2A (query rentability + tests) trước khi tạo/bật bốn API. Nếu D1 yêu cầu explicit publish, migration/workflow là dependency trước listing. Không cần thay pricing schema Option 1.

