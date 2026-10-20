# Test Matrix và UAT Evidence

## Automated verification

| Phạm vi | Bằng chứng | Kết quả |
|---|---|---|
| API client / query / route / lỗi | tests/publicBrowsingApi.test.ts: 11 Vitest cases | PASS |
| Home listing, filter, lỗi và stale request | tests/publicHome.test.tsx: 6 Vitest cases | PASS |
| Property detail, room list và room detail | tests/publicPropertyPages.test.tsx: 9 Vitest cases | PASS |
| Existing management contract regression | tests/managementContracts.test.cjs: 6 Node tests | PASS |
| Tổng | pnpm test qua pnpm 10.34.3 | 32/32 PASS |
| TypeScript | tsc --noEmit | PASS |
| Production bundle | pnpm build, Vite 8.0.5 | PASS |
| Coverage | 26 Vitest cases; chỉ include publicBrowsingApi.ts và constants/routes.ts | statements 89.01%, branches 85.9%, functions 100%, lines 89.3% |

Coverage không đại diện toàn bộ FE; chỉ hai module được cấu hình include.

## Tình huống được test

- Listing API fields/empty image, không render mock photo, distance, map pins hay monthly claim.
- Empty success và lỗi request được phân biệt; retry sau network error.
- Keyword, location, decimal price, RoomType, amenity, sort và pagination gửi đúng query.
- Giá invalid bị chặn phía client; filter mới reset page.
- Request cũ trả chậm không ghi đè kết quả mới.
- Property ID route được dùng cho detail và room list; repeated amenityIds và server paging.
- Property-level 404 khác với filtered empty room page.
- Room detail tải mới bằng RoomId; booking actions inactive; stale 404 cho phép refresh list.
- 400, generic 404, server/network error và abort được kiểm tra ở API client.

## Giới hạn UAT

Các test FE dùng fetch stub, không gọi BE đang chạy. Trong lần xác minh này API ở localhost:5102/7223 không lắng nghe. Trang Vite ở localhost:8443 trả HTML nhưng tab browser hiển thị trắng; Chrome DevTools không kết nối được. Vì vậy browser UAT và end-to-end FE→BE live được ghi NOT VERIFIED.

Coverage lần đầu với worker mặc định hết bộ nhớ; cấu hình test:coverage hiện chạy tuần tự bằng --maxWorkers=1 và lần cuối hoàn tất thành công. Project pin Node 22 trong .mise.toml nhưng môi trường chạy Node 26.7.0; xác minh chính xác trên Node 22 chưa thực hiện. Build có cảnh báo Node module.register() deprecated, không ảnh hưởng exit code.