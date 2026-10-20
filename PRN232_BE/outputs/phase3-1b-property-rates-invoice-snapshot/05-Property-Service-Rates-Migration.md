# Phase 3.1B — Migration 2: PropertyServiceRates

**Migration:** 20261019143615_AddPropertyServiceRates  
**Bảng:** RateId integer identity PK; PropertyId/ServiceId bắt buộc; Price numeric(18,2); UnitCode/BillingBasis text; EffectiveFrom date; EffectiveTo date nullable.

## Giá trị và thời gian

Mỗi giá gắn với (PropertyId, ServiceId). Sáu chế độ nghiệp vụ ánh xạ vào năm cặp mã: kWh/usage; m3/usage; room_month/fixed_room (Internet và rác); person_month/occupant; vehicle_month/vehicle. Giá >= 0, zero được phép cho miễn phí có xác nhận; không dùng float/double. EffectiveFrom phải là ngày đầu tháng; EffectiveTo, nếu có, phải sau EffectiveFrom và cũng là đầu tháng; khoảng áp dụng [from,to) nên các tháng kề nhau hợp lệ.

DB không thể kiểm tra chính sách động “không sớm hơn đầu tháng sau” theo giờ yêu cầu. Phase dịch vụ tạo/sửa giá phải chuyển thời gian Asia/Ho_Chi_Minh sang ngày nghiệp vụ và từ chối ngày trước tháng tiếp theo. Không cho đổi hồi tố.

## Ràng buộc và deletion

- FK Property/Service Restrict để giữ lịch sử giá.
- Index (PropertyId, ServiceId, EffectiveFrom) và index ngược ServiceId.
- Unique key (RateId, ServiceId) hỗ trợ composite FK từ dòng hóa đơn.
- CHECK cho giá, mã đơn vị/cơ sở, ngày đầu tháng và range hợp lệ.
- Exclusion constraint GiST trên PropertyId, ServiceId và daterange(EffectiveFrom, EffectiveTo, '[)') ngăn overlap, kể cả khi ghi đồng thời. NULL upper bound thành khoảng mở tới vô cực.
- Migration bật RLS, không tạo policy. Extension btree_gist không bị drop khi Down.

## PostgreSQL proof và giới hạn live

Test trên PostgreSQL 17.11 độc lập xác nhận giá cùng Service có thể khác theo Property; mã unit sai/giá âm/ngày giữa tháng/end không hợp lệ bị CHECK chặn; hai khoảng liền nhau và hai Property khác nhau hợp lệ; overlap/open-ended/concurrent conflict bị exclusion constraint chặn (SQLSTATE 23P01). Đây là đảm bảo DB thực tế cho migration đã áp ở test.

Live chưa cài btree_gist; catalog cho thấy extension trusted sẵn có và role audit có CREATE, nhưng chưa thực hiện CREATE live. Vì vậy migration có triển khai GiST thật trong test, còn khả năng cài extension tại live chưa được xác minh bằng thao tác. Không được hạ constraint thành SELECT-then-INSERT. PostgreSQL mô tả cách cài trusted extension và btree_gist trong tài liệu chính thức: [CREATE EXTENSION](https://www.postgresql.org/docs/17/sql-createextension.html), [btree_gist](https://www.postgresql.org/docs/17/btree-gist.html). Đây không thay thế kiểm chứng quyền live.

## Dữ liệu và consumers

Không có giá mẫu/seed; bảng mới rỗng sau migration. Services.Price vẫn là legacy catalog. Source hiện có các consumer đọc Service.Price trong ServiceCatalogService, RoomTypeServices/RoomTypeRepository và public room detail. Phase 3.1C phải chuyển public livingCosts sang PropertyServiceRates khi rate được cấu hình và hợp lệ; không fallback sang Services.Price. Không thêm Parking/people-fee catalog item trong migration.

SQL_REVIEW/02-AddPropertyServiceRates.sql chỉ để review; chưa chạy live.