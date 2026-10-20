# 02 — Backup, restore drill, and recovery readiness

**Status: BLOCKED — no project-specific recovery point or successful restore drill is evidenced.**

## Evidence

- PRN232 Database Settings displayed the project as Free Plan. Navigating to Database Backups redirected to Supabase sign-in; the scheduled-backup/PITR detail and latest recovery point were not visible.
- Supabase's current documentation says automatic daily backups are for Pro, Team, and Enterprise; it recommends regular exports and off-site backups for Free projects. Database backups also exclude Storage objects. [Supabase Database Backups](https://supabase.com/docs/guides/platform/backups)
- PostgreSQL 17.10 pg_dump.exe and pg_restore.exe are installed at C:\Program Files\PostgreSQL\17\bin but are not on PATH. They were not run because the configured TLS connection does not verify the server certificate.
- No backup file was created. No restore drill was run. No database dump or tenant/billing data was copied into the repository or reports.

## Recovery facts

| Item | Result |
|---|---|
| Dashboard backup/PITR point and retention | NOT VERIFIED |
| Logical backup from live | NOT CREATED |
| Restore drill to independent PostgreSQL | NOT RUN |
| Storage object recovery | NOT VERIFIED |
| RPO / RTO accepted by owner | NOT PROVIDED |

**Stop decision:** the prompt explicitly blocks live writes when backup or restore cannot be demonstrated. No live migration or seed was started. Project backup evidence and a successful independent restore drill are still required.