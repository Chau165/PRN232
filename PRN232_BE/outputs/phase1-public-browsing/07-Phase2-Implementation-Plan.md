# 07 — Kế hoạch Phase 2 (chưa triển khai)

## Thứ tự công việc

| ID | Mục tiêu | File/component dự kiến (đều mới hoặc sẽ sửa ở Phase 2) | Phụ thuộc | Ưu tiên / độ phức tạp | Tiêu chí nghiệm thu |
|---|---|---|---|---|---|
| P2-01 | Chốt publication và rentability | Quyết định nghiệp vụ; test cases | Owner/Product decisions D1–D4 | P0 / M | Có vocabulary, UTC/date semantics, hold policy và public scope được duyệt |
| P2-02 | Public DTO/service/repository | Application DTOs/Interfaces/Services; Infrastructure Repositories; DI Program.cs | P2-01 | P0 / L | Projection server-side; AsNoTracking; không trả Entity/PII |
| P2-03 | Property listing/aggregation | Public properties controller + repository query | P2-02 | P0 / L | Một card/Property; count distinct và min price từ cùng RentableRooms; pagination/sort ổn định |
| P2-04 | Property detail + rentable room list | Public property routes | P2-02, P2-03 | P0 / M | Chỉ public data; count/price/type options consistent; room list paged |
| P2-05 | Public room detail | Public rooms route/repository projection | P2-01, P2-02 | P0 / M | 404 nếu không tồn tại/không public/không eligible; data an toàn |
| P2-06 | Filter MVP | Repository predicates + Home form state | P2-03 | P1 / M | keyword/location/price/type/amenity áp cùng Room; validation 400 |
| P2-07 | FE API integration | API client mới, HomePage/SearchSection/cards/map, PropertyDetailPage, AvailableRoomsPage, RoomDetailModal | Public contracts ổn định | P1 / M | Xóa mock fields khỏi các flow được nối; trạng thái loading/empty/error |
| P2-08 | Media/catalog projections | RoomImages, RoomAmenities, RoomTypeServices, Equipment allow-list | P2-04, P2-05 | P1 / M | ảnh có fallback null; services gắn đúng RoomTypeId; không lộ equipment metadata |
| P2-09 | Sorting/paging polish | Public repositories + FE pagination | P2-03/P2-04 | P1 / M | trang 1–50, tie-break ID, totalCount chính xác |
| P2-10 | Geolocation/radius | Property coordinate schema decision, map provider/FE permission flow | P2-01; precision decision | P2 / L | radius kiểm thử bằng coords đủ chính xác; permission denied fallback; không có distance giả |

Tên file/controller cụ thể phải được chốt theo convention trong CODING_STANDARDS.md; danh sách trên là đề xuất mới, không phải file đang tồn tại. Không thay CRUD management scope trong task Phase 1.

## Test plan Phase 2

Chưa chạy test trong audit thiết kế. Các test dưới đây là kế hoạch; nhóm cần quyết định nghiệp vụ được đánh dấu **DECISION-DEPENDENT**.

1. Anonymous listing succeeds (sau visibility decision).
2. Anonymous Property Detail succeeds.
3. Anonymous Room Detail succeeds.
4. Tổng Property sau filter đúng.
5. Một card cho mỗi Property.
6. Không duplicate Property card khi join nhiều Room/Amenity/Image.
7. StartingPrice chỉ lấy eligible Rooms.
8. AvailableRoomCount chỉ đếm eligible Rooms.
9. Booking đang giữ loại khỏi listing — DECISION-DEPENDENT.
10. Contract hiệu lực loại khỏi listing — DECISION-DEPENDENT.
11. Cancelled Booking lịch sử không block — status rule cần xác nhận.
12. Contract hết hạn không block nếu không có blocker khác — end-date/status semantics cần xác nhận.
13. Property không còn phòng thuê được: default exclude; availableOnly=false trả 0/null nếu được cho phép.
14. RoomType.Price lấy theo RoomTypeId.
14a. Hai RoomTypes cùng tên Studio và ID/giá khác trả đúng giá cho Room từng tham chiếu.
14b. StartingPrice min đúng trên rentable set có duplicate TypeName.
14c. Dropdown nhóm logical TypeName; filter name khớp các ID; exact roomTypeId chỉ khớp một ID.
14d. Hai Studio khác ID giữ RoomTypeServices mapping riêng.
14e. Sửa giá Type A không đổi Room/Type B cùng tên nhưng ID khác.
15. Fees lookup qua RoomTypeServices, không gán unit hoặc cộng tổng sai.
16. RoomAmenities map chính xác cho Room.
17. Public Equipment DTO không trả private fields.
18. Keyword filter.
19. Price range lọc trên cùng eligible Room.
20. roomTypeName/exact ID filter.
21. Multiple amenity filter yêu cầu cùng Room có đủ IDs.
22. Combined price/type/amenity filter cùng Room.
23. Pagination và stable sorting.
24. Query invalid trả 400.
25. Missing Property/Room và hidden/non-rentable trả 404 phù hợp.
26. DTO không lộ Owner/tenant/Booking/Contract PII.
27. Existing management CRUD behavior không đổi.
28. Existing Auth/Maintenance/Notification tests vẫn pass.
29. Reserved status không bị quảng cáo và status handling đúng sau D4.
30. Future Contract, overlap, expired Booking được kiểm thử sau khi D2/D3 chốt.

Các test 9–12, 25 phần visibility, 29–30 phụ thuộc quyết định nghiệp vụ; không chuyển thành acceptance rule trước khi được duyệt.