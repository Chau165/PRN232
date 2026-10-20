# Phase 2A — Implementation report

## Kết quả

**READ-SIDE READY.** Backend có một rentability predicate dùng lại được, truy vấn phòng và projection count/price trên cùng rule. Booking reservation và chống double-booking vẫn **NOT END-TO-END READY** vì không có Booking write API.

Phân tích bám theo execution path Phase 2A bằng CodeGraph và source hiện tại: `RoomStatuses`, `RoomService.UpdateAsync`, Room/Booking/Contract/RoomType entities, `AppDbContext`, `RoomRepository`, `RoomsController`, interface và DI registrations. CodeGraph báo 132 source/config files trong index; phần ngoài phạm vi availability không được sửa.

## Thay đổi

- `RoomStatuses`: thêm `Reserved` vào constants và danh sách status hợp lệ. Không thêm transition tự động; không đổi quyền Admin/Manager hoặc owner-check hiện có.
- `RoomRentabilityPolicy`: tạo `Expression<Func<Rooms,bool>>` EF-translatable. Một `DateTimeOffset asOfUtc` được chuẩn hóa thành UTC một lần; Room chỉ qua nếu status chính xác `Available`, không có Booking blocker, và không có Contract chưa được biết là đã release.
- `RoomAvailabilityQueries`: gom query `AsNoTracking` để Room detail/list tương lai và property aggregate dùng chung predicate. Booking/Contract blockers dùng correlated `Any`/`NOT EXISTS`; không tải bảng liên quan vào memory.
- `GetRentableRoomsAsync`: projection `RentableRoomDto` chỉ có RoomId, PropertyId, RoomTypeId, TypeName, Price, RoomCode, AreaSqm; không trả Status hoặc Booking/Contract/customer data.
- `GetPropertyAvailabilityAsync`: một property row cho mỗi Property; count dùng LINQ `Distinct().Count()`, price là nullable `Min(RoomTypes.Price)` từ cùng rentable set. EF/Npgsql tối ưu `Distinct` trên khóa chính thành `COUNT(*)`; vì query bắt đầu từ một Room row, không join ảnh/amenities nên không nhân Room.
- `RoomService`/`IRoomService` expose hai read methods nội bộ cho controller Phase 2B tương lai; các controller hiện tại không được đổi response.
- Không sửa Program.cs, frontend, Auth/Maintenance/Notification production code, entity/schema, hay migration.

## TDD và kiểm thử

RED trước production change: test project biên dịch tới feature tests rồi dừng vì thiếu `RoomStatuses.Reserved`, hai availability methods và `RentableRoomDto` (CS0117/CS1061/CS0246). Sau implementation:

| Guarantee | Test | Kết quả |
|---|---|---|
| Available không blocker qua; Pending trước expiry block, tại equality/sau expiry không block; Deposited block dù expiry trước/sau; Cancelled không block | `GetRentableRoomsAsync_applies_room_booking_and_contract_blockers` | PASS — SQLite relational test |
| Active Contract trong ngày, tương lai, hoặc quá EndDate vẫn block; Contract status chưa biết block; Room Reserved/Occupied/Maintenance không qua | Cùng test | PASS |
| Pending hết hạn không làm Room qua nếu có Active Contract; nhiều Booking được xét; Booking status lạ/khác casing block | Cùng test | PASS |
| Count và MIN dùng cùng set; duplicate `Studio` IDs giữ giá riêng; Room giá thấp bị Booking block không ảnh hưởng MIN; một Room có nhiều Booking không bị đếm hai lần | `GetPropertyAvailabilityAsync_uses_one_rentable_set_for_count_and_minimum_price` | PASS — EF InMemory semantics |
| Không có Room rentable (hoặc không có Room) => count 0, price null | `GetPropertyAvailabilityAsync_returns_zero_and_null_when_no_room_is_rentable` | PASS |
| Npgsql sinh SQL server-side `NOT EXISTS`, count, MIN và không project Contract/Customer IDs | `GetPropertyAvailability_generates_postgresql_server_side_aggregates` | PASS — `ToQueryString`, không kết nối DB |
| Room projection chỉ chứa fields allow-list | `RentableRoomDto_contains_only_allowlisted_room_fields` | PASS |
| Reserved được update; owner/Admin path còn hiệu lực; người không phải owner, status/type/code/area sai vẫn bị từ chối; controller Update giữ roles `Admin,Manager` | `RoomUpdate_accepts_reserved_and_keeps_owner_and_role_checks` | PASS |

Thời điểm test dùng `DateTimeOffset` UTC+07 và expiry là UTC instant để kiểm tra so sánh timezone. SQLite không hỗ trợ `MIN(decimal)`, nên aggregate semantics chạy trên EF InMemory; Npgsql `ToQueryString()` kiểm tra query aggregate production provider. Không có endpoint mới nên không có E2E/browser test.

## Verification

- `dotnet restore PRN232_BE.sln`: PASS, mọi package đã up-to-date.
- `dotnet build PRN232_BE.sln --no-restore`: PASS, 0 warning, 0 error.
- `dotnet test ... --filter FullyQualifiedName~RoomAvailabilityTests`: PASS, 6/6.
- Full `dotnet test PRN232_BE.sln` chạy song song ban đầu có hai test Maintenance fail và test host stack overflow khi teardown; từng test fail PASS khi chạy riêng. Full suite sau đó chạy với runsettings tuần tự tạm trong `work/`: PASS, 150/150, 0 skipped. Cấu hình repo không bị đổi.
- Coverage full suite: tổng line coverage 15.61% (chưa đạt ngưỡng 80% toàn solution của TDD skill); `RoomRentabilityPolicy`, `RoomAvailabilityQueries` và bốn repository/service methods mới đạt 100%. Hai class lớn có code cũ chưa được test là `RoomService` 75% và `RoomRepository` 50.61%; các method Phase 2A trong đó đều 100%. Không mở rộng test/sửa các module ngoài scope để nâng coverage tổng.
- `git diff --check`: không có whitespace error ở tracked source diff; new source/test/report files được quét riêng, không có trailing whitespace hoặc thiếu newline. `06-Diff.patch` giữ nguyên các dòng context theo định dạng git diff.

## Giới hạn và trạng thái dữ liệu

- Live snapshot vẫn có một Room `Reserved`, ba Booking `Deposited` hết hạn theo snapshot Phase 1.5 và các Pending/status cũ. Query chỉ tính hiệu lực động; không thay đổi persisted status.
- Không có logic nào tự set/clear Room `Reserved`. Nếu Pending hết hạn, Room `Reserved` vẫn không rentable cho tới khi owner/admin chuyển status qua luồng quản lý hiện hành.
- Schema snapshot nhỏ: 9 Rooms, 5 Bookings, 3 Contracts; đây không phải bằng chứng chống race hay coverage mọi dữ liệu tương lai.
- Không chạy truy vấn ghi vào Supabase; không có migration, commit hoặc push.

## TDD evidence

Journeys được lấy trực tiếp từ prompt Phase 2A: Room chỉ được quảng cáo khi qua tất cả blocker; Property count/starting price mô tả cùng Room set; Room manager tiếp tục quản lý status được hỗ trợ theo quyền hiện tại.

RED là compile-time failure có nguyên nhân trực tiếp từ API/status còn thiếu. GREEN là 6/6 feature tests và 150/150 full tests chạy tuần tự. Không tạo checkpoint commit vì prompt Phase 2A cấm commit. Coverage test command và báo cáo là output trong `work/phase2a-coverage-final/`; không dùng nó làm bằng chứng E2E hoặc production readiness.
