# Phase 3.1B — Migration 1: Description và PropertyAmenities

**Migration:** 20261019143543_AddPropertyDescriptionAndAmenities  
**Phạm vi:** Entity/schema cục bộ và test PostgreSQL; không áp lên live.

## Thay đổi model

- Properties.Description: string? → PostgreSQL text NULL; không default, không backfill.
- PropertyAmenities: composite key (PropertyId, AmenityId); FK tới Properties và Amenities; index IX_PropertyAmenities_AmenityId.
- Entity navigation được thêm cho Property, Amenities và join entity; DbSet/Fluent mapping trong AppDbContext.
- RoomAmenities không thay đổi và vẫn mô tả tiện ích theo phòng. Không tạo PropertyAmenities từ tiện ích phòng.

FK của bảng liên kết dùng Cascade để khi Property/Amenity bị xóa chỉ xóa liên kết tương ứng. Không lan cascade sang RoomAmenities hay dữ liệu khác. Bảng bật RLS theo convention live; migration không thêm policy hay quyền ghi public.

## Kiểm thử cô lập

PostgreSQL test xác nhận nâng cấp giữ Property cũ với Description NULL; hai Property có thể cùng liên kết một Amenity; duplicate composite key bị chặn (23505); PropertyId không tồn tại bị FK chặn (23503). Bảng liên kết ban đầu rỗng sau migration. Migration Down có guard: từ chối nếu liên kết đã có hoặc Description chứa dữ liệu, tránh xóa dữ liệu mới.

## SQL

SQL_REVIEW/01-AddPropertyDescriptionAndAmenities.sql được sinh bằng migrations script sau AddPropertyPrimaryImageUrl. Script có DDL và ghi history cho môi trường đã được duyệt; tuyệt đối không chạy trên Supabase trong Phase này.

## Tác động

Không thêm API/DTO/FE. Các giá trị Description hiện có giữ nguyên NULL; tiện ích Property cần được nhập và xác minh riêng ở phase dữ liệu sau. PropertyAmenities không được suy luận từ RoomAmenities.