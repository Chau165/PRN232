# 05 — Live execution Gate B/C/D/E

**STOP trước Gate B. Không có live SQL write.**

| Gate | Trạng thái | Tác động |
|---|---|---|
| B — Maintenance reconciliation | SKIPPED | Không chạy index concurrent/history marker |
| C — Description + PropertyAmenities | SKIPPED | Không chạy DDL |
| D — PropertyServiceRates + btree_gist | SKIPPED | Không chạy extension/DDL |
| E — InvoiceDetails snapshot | SKIPPED | Không chạy DDL |

Lý do: backup/restore chưa đạt, password chưa rotate, rehearsal trên restore chưa chạy. Hash đúng không thay thế safety gates. Không dùng EF Down, không sửa Invoice/Booking/Contract/Maintenance, không phát hành hóa đơn.
