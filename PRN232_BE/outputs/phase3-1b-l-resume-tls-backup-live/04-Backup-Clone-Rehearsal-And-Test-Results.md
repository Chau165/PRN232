# 04 — Backup clone rehearsal và regression tests

**BLOCKED — không có bản backup để restore/rehearse.**

- Restore: NOT RUN. Gate B→C→D→E rehearsal trên bản restore: NOT RUN.
- Full regression của lượt resume: NOT RUN; không chạy PostgreSQL public-route tests trên live.
- Điều kiện 4 public-route PostgreSQL tests không bị skip trên isolated fixture chưa được chứng minh.

Kết quả lịch sử, không thay thế rehearsal hiện tại: artifact Phase 3.1B-L ghi 199 passed, 0 failed, 4 skipped trên PostgreSQL synthetic loopback; bốn PublicPropertyListingPostgreSqlTests thiếu fixture riêng. Artifact Phase 3.1C-DEV ghi một run khác 203 passed, 0 failed, 0 skipped. Không run nào dùng bản restore live hiện tại.
