# PostgreSQL cô lập — replay và regression

## Môi trường

- PostgreSQL local `17.10`, cluster mới trong `%TEMP%`, chỉ bind `127.0.0.1:55432`; test role là superuser `postgres`.
- Database fixture có dữ liệu synthetic `fixture.invalid`; không copy row hay credential từ Supabase.
- Supabase live là PostgreSQL `17.11`; do khác patch version và quyền, local replay không chứng minh hành vi Supabase extension policy.

## Kịch bản Gate B

1. EF tạo schema tới migration `20261019034905_AddPropertyPrimaryImageUrl`.
2. Fixture phản chiếu live: 3 Properties, 25 InvoiceDetails, 9 Logs (1 OldStatus NULL), 4 Reports, 5 Image metadata; `CreatedAt DEFAULT CURRENT_TIMESTAMP`; hai secondary index Reports/Images bị thiếu; Reconcile history row bị xóa nhưng marker migration sau vẫn còn.
3. Thử `dotnet ef database update` trên fixture: dừng với PostgreSQL `42P07` khi gặp `MaintenanceReports already exists`; transaction rollback, dữ liệu/index/history không đổi. Đây là tái hiện tình huống, không phải lỗi live.
4. Gate B candidate trên clone thiếu hai live status CHECK bị fail-closed; exit code 3, không tạo index và không thêm history. Sau khi clone được bổ sung hai CHECK cùng trạng thái RLS logs đúng snapshot, candidate hoàn tất: hai index tạo thành công, marker Reconcile ghi ở cuối.

## Replay C/D/E và dữ liệu

Chạy đúng ba SQL review hiện hành theo thứ tự C, D, E trên clone riêng; cả ba transaction commit thành công. Gate D cài `btree_gist 1.7` trong môi trường local superuser. Postflight READ ONLY xác nhận:

- History có đủ 8 migration, `ProductVersion=8.0.10`.
- Giữ nguyên 3 Properties; Description NULL cả 3; PropertyAmenities=0; PropertyServiceRates=0.
- InvoiceDetails vẫn 25; cả 25 dòng có sáu snapshot fields NULL.
- MaintenanceLogs=9, Reports=4, Images=5; 1 log có OldStatus NULL; CreatedAt default vẫn CURRENT_TIMESTAMP.
- Fingerprint trước/sau không đổi: InvoiceDetails `c7729e66b11571b2dd49535a4d30cdb9`; Logs `b320935a5ef3e9aceaa4d2ea41a70bea`; Reports `905ac593f8840ba1e22796793cb0fe18`; Images `5fb8592d5d2f5304d3007514958bd621`. Invoice fingerprint chỉ gồm năm cột legacy; các bảng Maintenance hash toàn bộ synthetic rows. Không đưa giá trị từng dòng vào log.
- Index Reports/Images và exclusion GiST đều `indisvalid=true`, `indisready=true`; RLS bật trên hai bảng mới, policy count 0.

## EF/build/API evidence

- `dotnet ef migrations list` trên clone cho thấy đủ 8 migration đã apply.
- `dotnet ef migrations has-pending-model-changes`: “No changes have been made to the model since the last migration.” Exit 0.
- `dotnet build PRN232_BE.sln --no-restore`: thành công, 0 warning / 0 error.
- Full backend suite sau khi dùng database fixture đã migrate: 200 passed, 0 failed, 0 skipped; TRX tổng hợp ở `%TEMP%\prn232-phase31br-tests\full-regression-seeded.trx`.
- Lần chạy test đầu tiên trước khi fixture được migrate có 196 pass / 4 fail do `42P01` (bảng `Rooms`/`Properties` chưa tồn tại). Sau khi setup fixture, chạy lại đạt 200/0; lỗi đầu tiên là setup test DB, không phải code regression.

Đây là bằng chứng build/test local. Không xác nhận kết nối API thực tế tới Supabase, FE visual UAT, phân quyền RLS của từng role live hoặc backup/restore readiness.
