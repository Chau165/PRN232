# Phase 3 Readiness và Next Steps

## Trạng thái

COMPLETE — PUBLIC FE FLOW DEVELOPMENT READY; PRODUCTION BLOCKED.

Phase 3A, 3B, 3C, 3D đã được thực hiện tuần tự. Bốn public read endpoints được nối vào Home, Property detail, room list và Room detail. FE build/typecheck/unit verification thành công.

## Bước tiếp theo trước production

1. Sửa và kiểm thử authorization cho anonymous POST /api/roles; hoàn tất review 17 anonymous legacy/reference GET ngoài public allow-list.
2. Lấy owner decision về RoomCode và zero-rentable-room 404.
3. Chốt public image provider/host/ACL; chốt public equipment policy và status vocabulary.
4. Xác nhận API origin, CORS và proxy/routing trên môi trường tích hợp.
5. Chạy browser E2E thật Home → Property → Rooms → Room detail với API chạy; xác nhận stale availability/404.
6. Chạy lại FE checks trên Node 22 theo .mise.toml và đánh giá browser accessibility/visual behavior trên browser hoạt động.

## Verification scope

- FE: 26 Vitest pass, 6 Node regression pass, typecheck pass, build pass, configured coverage pass.
- BE: không sửa và không chạy lại bộ test trong Phase 3; Phase 2E report ghi 198/198 backend tests pass và 4/4 PostgreSQL integration checks pass ở baseline.
- Live browser/API: NOT VERIFIED; API ports không lắng nghe và browser smoke không hoàn tất.
- DB, Booking write, Auth và chức năng quản trị: không thay đổi.
- Không commit, push hoặc deploy.