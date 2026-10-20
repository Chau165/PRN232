# 09 — API smoke, release và security blockers

- API smoke live: NOT RUN. Không bật feature flag, restart/deploy BE, sửa FE hoặc Billing Engine.
- Artifact Phase 3.1C-DEV cũ ghi enrichment flag mặc định OFF và FE chưa render các trường enriched; runtime chưa được kiểm tra trong lượt này.
- API behavior sau migration chưa xác minh vì không migrate.
- Security blockers trong báo cáo Phase 3.1C-DEV: anonymous POST /api/roles, legacy anonymous/owner-scope, RLS policy gaps. Chưa re-audit source/runtime trong lượt resume.
- Schema migration không đồng nghĩa production release-ready.
