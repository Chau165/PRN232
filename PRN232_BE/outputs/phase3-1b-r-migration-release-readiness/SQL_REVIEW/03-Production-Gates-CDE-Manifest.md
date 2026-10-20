# Manifest — SQL review Gate B/C/D/E

**Chỉ để review. Không phải phê duyệt chạy live.** Mọi SHA-256 dưới đây được tính từ bytes của file lúc chuẩn bị bàn giao. Thay đổi dù một byte cần review/hash/approval lại.

## Gate B — Reconcile Maintenance

| File | SHA-256 |
|---|---|
| `01-Gate-B-Maintenance-Reconciliation-REVIEW-ONLY.sql` | `d3dad4a20ab58aad4fbc374aa52a0508bd977f13ee5530475165d3104a1c0701` |

Tác động ứng viên: tạo hai secondary btree index bằng `CREATE INDEX CONCURRENTLY`, rồi ghi đúng một history marker trong transaction guard cuối. Không replay `Up()`, không thay đổi dữ liệu/cột/CHECK/RLS/default. Script yêu cầu psql variable `phase31br_gate_b_approved=true`; chỉ dùng sau phê duyệt ngoài-of-band của hash này, decisions và preflight mới.

## Gates C/D/E — nguồn SQL Phase 3.1B hiện có

| Thứ tự | File gốc | Migration ID | SHA-256 |
|---:|---|---|---|
| C | `outputs/phase3-1b-property-rates-invoice-snapshot/SQL_REVIEW/01-AddPropertyDescriptionAndAmenities.sql` | `20261019143543_AddPropertyDescriptionAndAmenities` | `7390649de04ed4e2b2220ac3b8ae0ac6a6e9f414feb41d3911ba51727d87d33a` |
| D | `outputs/phase3-1b-property-rates-invoice-snapshot/SQL_REVIEW/02-AddPropertyServiceRates.sql` | `20261019143615_AddPropertyServiceRates` | `8247c5208f02095cd288e4aa1c271b6e20f041a6c92712ccba3791ae759a509e` |
| E | `outputs/phase3-1b-property-rates-invoice-snapshot/SQL_REVIEW/03-AddInvoiceDetailBillingSnapshot.sql` | `20261019143633_AddInvoiceDetailBillingSnapshot` | `225902a76a91ed7f548318fc60efeaf14204ed634d91266d6155992c9d6cbc85` |

Ba file C/D/E hiện có transaction và ghi migration history theo đúng file. Chạy tuần tự C → D → E, mỗi file một lần sau Gate B. Gate D cài `btree_gist`. Cần approval một hash cho từng file; không chỉnh nội dung trong quá trình vận hành.

## Read-only scripts

| File | SHA-256 |
|---|---|
| `00-Live-READ-ONLY-Preflight.sql` | `5d4ff9aabad3113efc835854494ba485a996c3ae3816fd443e75085632a2216f` |
| `02-Postflight-READ-ONLY.sql` | `b020d7ee0b1f1994d77e0177e67453a4bc1b5c2145f3ac78992268d867dfad4c` |

Hai script trên chỉ đọc trong `BEGIN ... READ ONLY`, dùng để đối chiếu và không ghi trạng thái. Postflight phải chạy sau toàn bộ migration và so với preflight lưu lại.

## Exact review provenance

- Gate B design: `03-Gate-B-Reconciliation-Design-And-Owner-Choices.md`.
- Live equivalence: `02-Live-ReadOnly-Schema-Equivalence-Matrix.md`.
- Test replay: `06-Isolated-PostgreSQL-Replay-And-Regression.md`.
- Không chạy Gate B/C/D/E trên Supabase live trong Phase 3.1B-R.
