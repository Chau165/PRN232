# Phase 2E — Ma trận test và regression

## Test guarantees

| Hành vi | Test | Loại | Kết quả |
|---|---|---|---|
| Public Property listing anonymous, allow-list, validations, sort/page/filter cùng rentable Room | `PublicPropertyListingApiTests` | HTTP integration, InMemory | PASS |
| Property detail: count/price, generic 404 cho unknown/zero availability | `PublicPropertyListingApiTests` | HTTP integration, InMemory | PASS |
| Room page: route scoping, same-room filters, duplicate TypeName nhưng đúng RoomTypeId/price, page tie-break | `PublicPropertyListingApiTests` | HTTP integration, InMemory | PASS |
| Room detail: exact RoomType/service, scoped amenities, images/equipments rỗng, không status/PII, 404 boundary | `PublicPropertyListingApiTests` | HTTP integration, InMemory | PASS |
| Pending trước/đúng/sau expiry, Deposited, Cancelled, Active Contract, status lạ và Room status | `RoomAvailabilityTests` + `PublicPropertyListingApiTests` | Unit/API | PASS |
| Bốn route HTTP chạy qua Npgsql, không bearer; Property → room page → room detail; response allow-list | `Four_public_browsing_routes_work_over_read_only_postgresql_without_authentication` | HTTP + PostgreSQL thật | PASS |
| Session của API host là READ ONLY trước request; không có write trên DB | Cùng test; `SHOW default_transaction_read_only` = `on` | PostgreSQL guard | PASS |
| Anonymous management `POST /api/rooms` bị chặn | Cùng test; HTTP `401` | Negative auth | PASS |
| SQL translation, no OwnerId/CustomerId/private image/equipment query, SELECT bounds | 3 provider tests trong `PublicPropertyListingPostgreSqlTests` | Npgsql repository | PASS |
| JWT/role authorization trên các route maintenance/users hiện có | `MaintenanceFlowApiTests` | API security regression | PASS |

## Commands đã chạy

- `dotnet restore PRN232_BE.sln --verbosity minimal` — thành công; tất cả project up-to-date.
- `dotnet build PRN232_BE.sln --no-restore --verbosity minimal` — thành công, 0 warning, 0 error.
- Focused: `dotnet test PRN232_BE.sln --no-build --no-restore --settings 'C:\Users\Surface\Documents\Codex\2026-10-18\b\work\phase2b-sequential.runsettings' --results-directory 'C:\Users\Surface\Documents\Codex\2026-10-18\b\work\phase2e-test-results\focused-final' --logger 'console;verbosity=minimal' --filter 'FullyQualifiedName~PublicPropertyListingApiTests|FullyQualifiedName~RoomAvailabilityTests|FullyQualifiedName~MaintenanceFlowApiTests|FullyQualifiedName~PublicPropertyListingPostgreSqlTests'` — **112/112 pass, 0 skip**.
- Full: `dotnet test PRN232_BE.sln --no-build --no-restore --settings 'C:\Users\Surface\Documents\Codex\2026-10-18\b\work\phase2b-sequential.runsettings' --results-directory 'C:\Users\Surface\Documents\Codex\2026-10-18\b\work\phase2e-test-results\full-final' --logger 'console;verbosity=minimal'` — **198/198 pass, 0 skip**. During this command only, PowerShell set `PRN232_TEST_POSTGRES_CONNECTION` from the configured `ConnectionStrings__DefaultConnection`; its value was not printed.
- PostgreSQL tests were included in the focused/full runs — **4/4 pass**, comprising 3 repository/provider tests and 1 HTTP test that calls all four endpoints.
- `git diff --check` — exit 0; chỉ có cảnh báo newline conversion trên hai file đã modified từ trước (`.codegraph/.gitignore`, `Program.cs`).

## TDD và coverage

Chỉ thêm test integration; không có production code/behavior mới, nên không báo cáo RED cho một bug không tồn tại và không chạy coverage tổng thể. Hai lỗi harness ban đầu (compile thiếu JSON extension; server bỏ qua startup-option read-only) được sửa trước lần pass; lần thứ hai dừng tại `SHOW ... = off`, chưa gọi route. Không tính chúng là business RED. Test cuối xác nhận session `on` rồi mới chạy API.

Không chạy FE unit/E2E vì Phase 2E không sửa FE; frontend hiện không có test script/Playwright dependency. Không có test bị skip trong lần full cuối. Không tuyên bố coverage solution đạt 80%.
