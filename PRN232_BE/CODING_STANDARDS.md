# Coding Standard — PRN232_BE

Tài liệu này áp dụng cho tất cả thành viên khi code backend. Mục đích: code của 5 người
nhìn như 1 người viết, và khi thầy review thì convention đặt tên nhất quán giữa các module.

Module **Roles** là mẫu tham khảo cho DTO, Service và Controller:
(`PRN232.Application/{DTOs,Interfaces,Services}/Role*.cs`,
`PRN232.Api/Controllers/RolesController.cs`). Không sao chép cách Repository của module này
chiếu trực tiếp sang API DTO; Repository phải tuân thủ quy tắc Entity/Read Model tại mục 1.

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
  - `ReadModels/` — kiểu dữ liệu chỉ dùng nội bộ Application cho các truy vấn đọc phức tạp;
    không dùng API response DTO làm kiểu trả về Repository.
  - `Interfaces/` — `IXxxRepository`, `IXxxService` (contract, chưa có implementation).
  - `Services/` — implementation của `IXxxService`, gọi qua `IXxxRepository` (không gọi
    `AppDbContext` trực tiếp ở layer này). Service giữ nghiệp vụ và chuyển đổi DTO ↔ Entity/
    Read Model ↔ response DTO.

- **`PRN232.Infrastructure`** — chi tiết kỹ thuật: EF Core, Postgres, migrations.
  - `Data/AppDbContext.cs` — DbContext + Fluent API config (`OnModelCreating`).
  - `Repositories/` — implementation của `IXxxRepository`, là chỗ DUY NHẤT được query
    `AppDbContext` trực tiếp. CRUD đơn giản trả Entity; query chỉ đọc dùng `AsNoTracking()`,
    còn Entity cần cập nhật phải được tracking đến `SaveChangesAsync()`.
  - Query đọc có quan hệ được phép dùng `Include`/`ThenInclude` hoặc EF projection sang
    Application Read Model khi cần giới hạn cột, bảo toàn hiệu năng hoặc tránh tải dữ liệu nhạy
    cảm. Chỉ giữ projection đặc thù khi có lý do cụ thể; không projection sang API DTO trong
    Repository.
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

### Quy tắc mapping và ranh giới Repository

- Repository không phụ thuộc `PRN232.Application.DTOs` và không trả API request/response DTO.
  CRUD cơ bản ưu tiên `Entity`; query đọc phức tạp có thể trả `Application/ReadModels` hoặc giữ
  projection tối ưu có lý do được ghi rõ.
- Service chịu trách nhiệm map Entity/Read Model thành response DTO và map request DTO thành
  Entity. Dùng các phương thức convert riêng (`ConvertToEntity`, `ApplyUpdateToEntity`,
  `ConvertToDto`) cho ánh xạ thuần túy.
- Validation, authorization, ownership, trạng thái nghiệp vụ, defaults và normalization tiếp
  tục ở Service/layer hiện có; mapper không quyết định nghiệp vụ.
- Update áp dụng lên Entity đang tracking. Chỉ gán trường hiện được phép sửa; không thay primary
  key, foreign key, navigation, status hoặc concurrency token ngoài quy tắc nghiệp vụ hiện có.
- Filtering, sorting, pagination, aggregate và phân quyền theo dữ liệu phải tiếp tục chạy phía
  database. Không materialize toàn bảng để lọc ở Service, không tạo N+1, không tải cột nhạy cảm
  không cần thiết.
- Thay đổi Repository nội bộ không được làm đổi route, HTTP verb/status, request/response DTO,
  JSON camelCase/nullability, authorization hoặc error contract.

## 3. Checklist khi thêm 1 module mới

1. `Domain/Entities/Xxx.cs` — nếu chưa có (đa số entity đã có sẵn, chỉ cần check `HasKey`).
2. `Application/DTOs/XxxDto.cs` (+ `CreateXxxDto`, `UpdateXxxDto` nếu cần).
3. `Application/Interfaces/IXxxRepository.cs`, `IXxxService.cs`.
4. `Infrastructure/Repositories/XxxRepository.cs` — implement; CRUD trả Entity, read query
   liên quan dùng `Application/ReadModels` hoặc projection tối ưu có lý do.
5. `Application/Services/XxxService.cs` — implement, gọi qua `IXxxRepository`, giữ nghiệp vụ và
   mapping DTO/Entity/Read Model.
6. `Api/Controllers/XxxController.cs`.
7. Đăng ký DI trong `PRN232.Api/Program.cs`:
   ```csharp
   builder.Services.AddScoped<IXxxRepository, XxxRepository>();
   builder.Services.AddScoped<IXxxService, XxxService>();
   ```
8. Thêm unit/integration tests cho mapping, EF tracking, JSON/API contract, authorization và
   query behavior liên quan; kiểm tra không phát sinh N+1 hoặc tải dữ liệu nhạy cảm.

## 4. Quy tắc readability và control flow

- Ưu tiên method body có braces cho validation, nghiệp vụ, authorization và mapping nhiều trường.
  Giữ expression-bodied member khi biểu thức ngắn và đọc rõ ngay.
- Converter dài dùng kiểu đích tường minh và `return new ConcreteDto(...)`; giữ nguyên toàn bộ
  constructor argument, thứ tự trường, nullable, decimal precision và collection ordering.
- Điều kiện nghiệp vụ, role scope, null guard và notification creation dài nên dùng `if/else` để
  thể hiện từng nhánh. Đọc receiver một lần và giữ nguyên short-circuit, thứ tự đánh giá, side
  effects, exception type/message và lazy fallback.
- Tránh ternary lồng nhau hoặc ternary dài. Ternary ngắn, rõ nghĩa vẫn được giữ.
- Giữ `??` và `?.` khi chúng thể hiện fallback hoặc null propagation đơn giản. Chuyển `?? throw`
  dài sang guard tường minh chỉ sau khi kiểm tra thứ tự gọi và thời điểm ném exception.
- Dùng target-typed `new()` khi kiểu đích rõ trong ngữ cảnh; dùng kiểu cụ thể khi việc suy luận
  làm mapper hoặc khởi tạo dữ liệu khó đọc.
- Chuẩn hóa primary constructor cho DI có chọn lọc khi field và dependency dễ hiểu hơn. Không
  chuyển hàng loạt positional record, exception declaration hoặc constructor thể hiện rõ contract.
- Giữ switch expression, LINQ lambda, nullable annotation, EF Core expression tree và SQL
  projection khi chúng rõ nghĩa hoặc cần thiết cho dịch SQL. Không chuyển query sang xử lý phía
  client để đạt style.
- Không kéo dài code nếu không làm luồng điều khiển, kiểu dữ liệu hoặc mapping rõ hơn.
