# Coding Standard — PRN232_BE

Tài liệu này áp dụng cho tất cả thành viên khi code backend. Mục đích: code của 5 người
nhìn như 1 người viết, và khi thầy review thì convention đặt tên nhất quán giữa các module.

Module mẫu để tham khảo/copy khi bắt đầu module mới: **Roles**
(`PRN232.Application/{DTOs,Interfaces,Services}/Role*.cs`,
`PRN232.Infrastructure/Repositories/RoleRepository.cs`,
`PRN232.Api/Controllers/RolesController.cs`).

## 1. Kiến trúc N-layer

Solution gồm 4 project, phụ thuộc một chiều (mũi tên = "tham chiếu/biết tới"):

```
PRN232.Api  --->  PRN232.Application  --->  PRN232.Domain
   |                                            ^
   v                                            |
PRN232.Infrastructure  ----------------------------
```

- **`PRN232.Domain`** — entity (POCO ánh xạ 1-1 ra bảng DB). Không reference project nào khác,
  không có package EF/DI/HTTP gì cả. Chỉ properties + navigation properties.
  → Thêm bảng mới thì thêm class ở `Domain/Entities/`.

- **`PRN232.Application`** — business logic, không biết DB thật sự chạy bằng Postgres hay gì.
  - `DTOs/` — các record dùng để nhận/trả data qua API (không bao giờ trả thẳng Entity ra ngoài).
  - `Interfaces/` — `IXxxRepository`, `IXxxService` (contract, chưa có implementation).
  - `Services/` — implementation của `IXxxService`, gọi qua `IXxxRepository` (không gọi
    `AppDbContext` trực tiếp ở layer này).

- **`PRN232.Infrastructure`** — chi tiết kỹ thuật: EF Core, Postgres, migrations.
  - `Data/AppDbContext.cs` — DbContext + Fluent API config (`OnModelCreating`).
  - `Repositories/` — implementation của `IXxxRepository`, là chỗ DUY NHẤT được query
    `AppDbContext` trực tiếp. Dùng pattern EF projection: `.Select(x => new XxxDto(...))`
    ngay trong query (xem `RoleRepository`), không load Entity đầy đủ rồi map tay ở Service.
  - `Migrations/` — sinh bằng `dotnet ef migrations add ... --project PRN232.Infrastructure
    --startup-project PRN232.Api`, không sửa tay.

- **`PRN232.Api`** — chỉ có Controllers + `Program.cs` (DI, middleware, Swagger, JWT...).
  Controller chỉ gọi `IXxxService`, không tự query DB, không chứa business logic.

### Gotcha khi thêm Entity mới

EF Core tự nhận primary key nếu property tên là `Id` hoặc `<TênClass>Id`. Entity của mình
đặt tên class **số nhiều** (`Amenities`, `Rooms`...) nhưng property khóa chính **số ít**
(`AmenityId`, `RoomId`...) nên EF KHÔNG tự nhận ra — phải khai rõ trong
`AppDbContext.OnModelCreating`:

```csharp
modelBuilder.Entity<Amenities>().HasKey(x => x.AmenityId);
```

Quên dòng này sẽ gặp lỗi `requires a primary key to be defined` lúc `dotnet ef migrations add`.

## 2. Quy tắc đặt tên API

- **Route**: danh từ số nhiều, viết thường, dùng route mặc định của controller —
  `[Route("api/[controller]")]` → `RolesController` ra `/api/roles`. Không đặt route chứa động từ
  (không viết `/api/getAllRoles`).
- **HTTP verb theo đúng REST**:
  | Hành động            | Verb     | Route               |
  |-----------------------|----------|----------------------|
  | Lấy danh sách         | `GET`    | `/api/roles`         |
  | Lấy 1 item            | `GET`    | `/api/roles/{id}`    |
  | Tạo mới               | `POST`   | `/api/roles`         |
  | Sửa toàn bộ           | `PUT`    | `/api/roles/{id}`    |
  | Sửa 1 phần            | `PATCH`  | `/api/roles/{id}`    |
  | Xoá                   | `DELETE` | `/api/roles/{id}`    |
- **Resource lồng nhau** đi theo quan hệ cha-con: ví dụ danh sách phòng của 1 khu trọ là
  `/api/properties/{propertyId}/rooms`, không phải `/api/rooms-by-property/{id}`.
- **Controller**: `PascalCase` + hậu tố `Controller` (`RolesController`), kế thừa
  `ControllerBase`, có `[ApiController]`.
- **Action method trong Controller**: `GetAll`, `GetById`, `Create`, `Update`, `Delete` — không
  lặp lại tên resource trong tên method (không viết `GetAllRoles`, chỉ `GetAll`).
- **DTO**:
  - Đọc dữ liệu → `XxxDto` (ví dụ `RoleDto`).
  - Tạo mới → `CreateXxxDto`.
  - Cập nhật → `UpdateXxxDto`.
  - Không bao giờ expose Entity (`Models`/`Domain.Entities`) ra ngoài Controller — luôn qua DTO.
- **Interface/Service/Repository**: `IXxxRepository` / `XxxRepository`, `IXxxService` /
  `XxxService` — tên `Xxx` là tên nghiệp vụ số ít hoặc số nhiều theo đúng tên Entity liên quan
  (ví dụ entity `Bookings` → `IBookingRepository`/`BookingRepository`, không bắt buộc giữ số
  nhiều ở tên service/repo).
- **Async method**: luôn hậu tố `Async` (`GetAllAsync`, `AddAsync`, `SaveChangesAsync`).

## 3. Checklist khi thêm 1 module mới (copy pattern Roles)

1. `Domain/Entities/Xxx.cs` — nếu chưa có (đa số entity đã có sẵn, chỉ cần check `HasKey`).
2. `Application/DTOs/XxxDto.cs` (+ `CreateXxxDto`, `UpdateXxxDto` nếu cần).
3. `Application/Interfaces/IXxxRepository.cs`, `IXxxService.cs`.
4. `Infrastructure/Repositories/XxxRepository.cs` — implement, dùng EF projection.
5. `Application/Services/XxxService.cs` — implement, gọi qua `IXxxRepository`.
6. `Api/Controllers/XxxController.cs`.
7. Đăng ký DI trong `PRN232.Api/Program.cs`:
   ```csharp
   builder.Services.AddScoped<IXxxRepository, XxxRepository>();
   builder.Services.AddScoped<IXxxService, XxxService>();
   ```
