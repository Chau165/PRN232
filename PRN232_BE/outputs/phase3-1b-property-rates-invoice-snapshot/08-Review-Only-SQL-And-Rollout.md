# Phase 3.1B — SQL review và kế hoạch rollout

**Tất cả script migration bên dưới là REVIEW ONLY. Không chạy trên Supabase.** Gate B–E chưa được phê duyệt trong yêu cầu này.

## SQL_REVIEW inventory

| File | Mục đích | Trạng thái |
|---|---|---|
| 00-Maintenance-Reconciliation-Preflight-DRAFT-READ-ONLY.sql | Transaction repeatable-read read-only, history/catalog/count/RLS preflight | Chỉ SELECT/ROLLBACK; candidate index chỉ nằm trong comment |
| 01-AddPropertyDescriptionAndAmenities.sql | Migration 20261019143543 | DDL + history insert; review only |
| 02-AddPropertyServiceRates.sql | Migration 20261019143615 | DDL, btree_gist, exclusion constraint + history insert; review only |
| 03-AddInvoiceDetailBillingSnapshot.sql | Migration 20261019143633 | Cột nullable, FK/index/check + history insert; review only |

Các script 01–03 được sinh bằng EF migrations script với range ngay sau 20261019034905_AddPropertyPrimaryImageUrl, theo thứ tự 1→2→3. Script có BEGIN/COMMIT theo EF và tự ghi migration history nếu được chạy; do vậy không phải file chỉ-đọc và không được gửi vào production executor trước phê duyệt riêng. Không có script reconcile history Maintenance.

## Thứ tự release đề xuất — chưa được duyệt

1. Gate B: xác minh backup/PITR và restore; preflight lại toàn bộ Maintenance schema; quyết định CreatedAt default; xử lý index theo định nghĩa thực tế; reconcile có điều kiện chỉ sau review DBA/owner. Không INSERT history mù.
2. Gate C: thêm Description/PropertyAmenities; xác minh Description NULL, bảng liên kết rỗng, PK/FK/index/RLS.
3. Gate D: cài btree_gist và tạo rates; xác minh quyền/extension/constraint/locking trước. Bảng rates phải rỗng sau migration.
4. Gate E: thêm nullable InvoiceDetails snapshot; xác minh count/Amount/Description/ItemType cũ không đổi, 25 lines cũ vẫn NULL snapshot.
5. Chạy postflight, so khớp __EFMigrationsHistory với source sau mỗi cổng riêng; xác nhận RLS/policy, FK, indexes, constraint và aggregate counts.

Nếu Gate B thất bại, không chạy C–E bằng EF vì pending migration cũ có thể được chạy trước. Mỗi Gate C–E cần approval cho chính xác SQL, thời gian khóa, kế hoạch rollback và cửa sổ triển khai. Không coi yêu cầu chuẩn bị local là approval ghi live.

## Lock và phục hồi

ALTER TABLE / ADD FK/index có thể lấy lock; rà soát production load và statement timeout trong release riêng. Không dùng EF Down làm phương án hoàn tác sau khi dữ liệu mới được ghi: các migration có guard chống xóa dữ liệu mới. Ưu tiên app rollback tương thích schema, sau đó forward fix có kiểm soát. Khôi phục backup chỉ theo kế hoạch recovery đã thử, không thay thế xác minh.

## Postflight yêu cầu

Xác nhận đúng ba migration ID được áp; số Property hiện hữu giữ nguyên; Description không tự điền; PropertyAmenities/PropertyServiceRates không có seed; 25 invoice lines giữ nguyên; RLS giữ bật và không mở public write; exclusion constraint có mặt và btree_gist dùng được; không phát sinh dữ liệu PII trong log. Đây là checklist đề xuất, chưa chạy live.