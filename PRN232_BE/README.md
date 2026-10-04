# PRN232_BE — Backend Hệ thống Quản lý Phòng Trọ

ASP.NET Core Web API, 1 project duy nhất theo kiểu MVC quen thuộc (Controllers/Models/
Services), EF Core Code-First + Migrations, PostgreSQL (Supabase).

## Cấu trúc project

```
PRN232_BE/
├─ PRN232_BE.sln
├─ PRN232_BE.csproj
├─ Program.cs                  cấu hình DbContext, Swagger, JWT, CORS, Rate Limiting, DI
├─ appsettings.json
├─ Controllers/                HTTP endpoints (giống Controller trong MVC)
├─ Models/                     Entity — map 1-1 với 21 bảng trong ERD (thay cho "Models" của MVC truyền thống)
├─ Data/                       AppDbContext (Fluent API) + Migrations (tạo sau khi chạy `dotnet ef migrations add`)
├─ DTOs/                       Request/response shape riêng, không lộ Entity ra ngoài
├─ Interfaces/                 Contract cho Repository + Service (để dễ test/thay thế)
├─ Repositories/                truy vấn AppDbContext
└─ Services/                   business logic, gọi Repository
```

Luồng gọi: `Controller → Service (interface) → Repository (interface) → AppDbContext`.
Không có Views vì FE là React riêng (`PRN232_FE`), backend chỉ trả JSON — về bản chất
vẫn chạy trên framework MVC của ASP.NET Core, chỉ thiếu phần "V".

Module mẫu viết sẵn đầy đủ (copy đúng pattern cho module của bạn): **Roles**
— `DTOs/RoleDto.cs`, `Interfaces/IRoleRepository.cs` + `IRoleService.cs`,
`Repositories/RoleRepository.cs`, `Services/RoleService.cs`,
`Controllers/RolesController.cs`, và đăng ký DI trong `Program.cs`.

## Setup lần đầu (máy ai cũng làm 1 lần)

1. Cài [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) nếu chưa có.
2. Clone repo, mở `PRN232_BE.sln`.
3. Restore package: `dotnet restore` (project này build trong môi trường không có mạng
   nên **chưa restore được** — máy bạn có mạng bình thường thì bước này tự chạy OK).
4. Cấu hình connection string Supabase — **không gõ thẳng vào `appsettings.json`**,
   dùng User Secrets để tránh lộ password khi push code:

   ```bash
   dotnet user-secrets init
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=xxx.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=xxx;SSL Mode=Require;Trust Server Certificate=true"
   dotnet user-secrets set "Jwt:Key" "<một chuỗi random tối thiểu 32 ký tự>"
   ```

   Connection string lấy trong Supabase Dashboard → Project Settings → Database →
   Connection string (cổng 5432 cho kết nối trực tiếp, hoặc 6543 cho pooler).

5. Tạo migration đầu tiên và áp dụng lên Supabase:

   ```bash
   dotnet tool install --global dotnet-ef   # nếu chưa có
   dotnet ef migrations add InitialCreate
   dotnet ef database update
   ```

6. Chạy thử: `dotnet run`, mở `https://localhost:7010/swagger` (xem cổng thật trong
   `Properties/launchSettings.json`). Gọi `GET /api/health/db` — trả về
   `"database": "connected"` nghĩa là mọi thứ đã thông.

## Chạy bằng Docker (tuỳ chọn, local)

```bash
cp .env.example .env     # điền connection string / JWT key thật vào .env
docker compose up --build
```

API chạy ở `http://localhost:8080`. Không có service Postgres trong
`docker-compose.yml` vì database dùng Supabase (cloud).

## Khi phát hiện vấn đề schema lúc test (đúng flow migration/code-first)

Sửa trực tiếp trong class ở `Models/` hoặc `OnModelCreating` của `Data/AppDbContext.cs`, rồi:

```bash
dotnet ef migrations add <TenMoTaThayDoi>
dotnet ef database update
```

Không sửa tay SQL trên Supabase — mọi thay đổi schema đi qua migration để cả nhóm
đồng bộ qua Git.

## Phân module cho từng thành viên

Mỗi module tạo theo đúng khuôn Roles ở trên (DTO → Interface Repository/Service →
Repository/Service → Controller → đăng ký DI trong `Program.cs`), làm trên nhánh
riêng, mở PR để merge:

- Auth & User (Roles, Users, IdentityDocuments)
- Property & Room (KhuTro, RoomTypes, Rooms, RoomImages, Amenities, Equipments)
- Booking & Contract (Bookings, Contracts, UtilityReadings)
- Invoice & Payment (Invoices, InvoiceDetails, Payments)
- Maintenance & Notification (MaintenanceRequests, MaintenanceLogs, Notifications) + nối API thật vào FE (PRN232_FE) thay cho mock data

## Lưu ý

- `appsettings.json` chỉ chứa placeholder, không có secret thật — secret thật nằm
  trong User Secrets (local) hoặc biến môi trường (khi deploy lên Railway).
- Entity đặt tên số nhiều trùng tên bảng trong ERD (vd. `Contracts`, `Payments`) để
  khớp 1-1 với thiết kế đã chốt, tránh lệch khi review.
- 4 cặp FK kép (Users→Contracts, Users→IdentityDocuments, Users→MaintenanceRequests,
  Users→MaintenanceLogs) đã cấu hình `OnDelete(DeleteBehavior.Restrict)` trong
  `Data/AppDbContext.cs` để tránh lỗi multiple cascade paths khi migrate.
