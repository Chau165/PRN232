# 02 — Backup manifest và restore drill

**BLOCKED — chưa tạo backup; chưa restore.**

- DB live báo kích thước 12,842,675 bytes; pg_dump/pg_restore 17.10 sẵn có, cùng major với server 17.11.
- Thư mục bảo vệ ngoài repo: `C:\Users\Surface\AppData\Local\PRN232\secure-backups\phase3-1b-l-resume-20261020`; ACL chỉ Windows user hiện tại và SYSTEM; hiện 0 file.
- Automatic tool safety review từ chối lệnh tạo tạm PGPASSFILE từ cấu hình hiện có, trả thông báo “blocked by policy”. Bị chặn trước khi chạy; pg_dump chưa thực thi. Không chuyển password sang command line/environment để vượt chặn.
- Backup file/hash/manifest: NOT CREATED. Restore vào PostgreSQL độc lập: NOT RUN. Rehearsal: NOT RUN.
- Dashboard backup/PITR chưa refresh trong lượt này. Báo cáo cũ nêu Free Plan/sign-in, không xác nhận trạng thái hiện tại. RPO/RTO, Storage, Auth secrets và platform state chưa được xác minh.

Theo prompt, thiếu backup có restore drill thì dừng trước rehearsal và live DDL.

## Kiểm tra backup sau khi kết nối thành công — 2026-10-20

- `pg_dump` và `pg_restore` 17.10 có tại `C:\Program Files\PostgreSQL\17\bin`; server PostgreSQL 17.11.
- Không có `PGPASSFILE` trong User/Process và file mặc định `%APPDATA%\postgresql\pgpass.conf` chưa tồn tại. Thư mục backup vẫn trống (0 file).
- Backup chưa chạy. Automatic safety review trước đó đã chặn việc provision PGPASSFILE tạm từ connection string; không dùng command-line password hoặc cách khác để né.

Cần owner provision PGPASSFILE có ACL an toàn và báo path/ready status. Chỉ sau đó mới chạy backup, hash/manifest, restore độc lập, migration rehearsal và regression.
