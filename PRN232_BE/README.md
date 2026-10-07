# PRN232_BE — Backend Hệ thống Quản lý Phòng Trọ

ASP.NET Core Web API, kiến trúc N-layer (4 project), EF Core Code-First + Migrations,
PostgreSQL (Supabase).

Quy ước đặt tên API, folder nào làm gì chi tiết hơn: xem [`CODING_STANDARDS.md`](./CODING_STANDARDS.md)
— đọc trước khi bắt đầu module mới.

## Cấu trúc project

```
PRN232_BE/
├─ PRN232_BE.sln
├─ PRN232.Domain/               Entity — map 1-1 với 21 bảng trong ERD
│  └─ Entities/
├─ PRN232.Application/          Business logic (không biết DB chạy bằng gì)
│  ├─ DTOs/                     Request/response shape riêng, không lộ Entity ra ngoài
│  ├─ Interfaces/               Contract IXxxRepository / IXxxService
│  └─ Services/                 Implementation IXxxService, gọi qua IXxxRepository
├─ PRN232.Infrastructure/       Chi tiết kỹ thuật: EF Core, Postgres
│  ├─ Data/                     AppDbContext (Fluent API)
│  ├─ Repositories/             Implementation IXxxRepository — nơi DUY NHẤT query AppDbContext
│  └─ Migrations/               sinh bằng dotnet ef, không sửa tay
└─ PRN232.Api/                  Controllers + Program.cs (DI, Swagger, JWT, CORS, Rate limiting)
   ├─ Controllers/
   └─ Program.cs
```

Luồng gọi: `Controller → Service (interface) → Repository (interface) → AppDbContext`.
Không có Views vì FE là React riêng (`PRN232_FE`), backend chỉ trả JSON.

Module mẫu viết sẵn đầy đủ (copy đúng pattern cho module của bạn): **Roles**
— `PRN232.Application/DTOs/RoleDto.cs`, `PRN232.Application/Interfaces/IRoleRepository.cs`
+ `IRoleService.cs`, `PRN232.Infrastructure/Repositories/RoleRepository.cs`,
`PRN232.Application/Services/RoleService.cs`, `PRN232.Api/Controllers/RolesController.cs`,
và đăng ký DI trong `PRN232.Api/Program.cs`.

## Setup lần đầu (máy ai cũng làm 1 lần)

1. Cài [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) nếu chưa có.
2. Clone repo, mở `PRN232_BE.sln` (chứa 4 project ở trên).
3. Restore package: `dotnet restore`.
4. Cấu hình connection string Supabase — **không gõ thẳng vào `appsettings.json`**,
   dùng User Secrets để tránh lộ password khi push code (chạy trong `PRN232.Api/`,
   vì `UserSecretsId` khai ở đó):

   ```bash
   cd PRN232.Api
   dotnet user-secrets init
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=xxx.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=xxx;SSL Mode=Require;Trust Server Certificate=true"
   dotnet user-secrets set "Jwt:Key" "<một chuỗi random tối thiểu 32 ký tự>"
   ```

   Connection string lấy trong Supabase Dashboard → Project Settings → Database →
   Connection string (cổng 5432 cho kết nối trực tiếp, hoặc 6543 cho pooler).

5. Áp dụng migration có sẵn lên Supabase (đã có `InitialCreate` + rename, không cần tạo lại):

   ```bash
   dotnet tool install --global dotnet-ef   # nếu chưa có
   dotnet ef database update --project PRN232.Infrastructure --startup-project PRN232.Api
   ```

   Nếu `dotnet ef database update` bị treo/timeout (đã gặp với kết nối trực tiếp Supabase từ
   một số mạng), dùng cách thay thế: sinh script SQL rồi paste thủ công vào Supabase SQL Editor:

   ```bash
   dotnet ef migrations script --idempotent -o migration.sql --project PRN232.Infrastructure --startup-project PRN232.Api
   ```

6. Chạy thử: `dotnet run --project PRN232.Api`, mở `https://localhost:7010/swagger` (xem cổng
   thật trong `PRN232.Api/Properties/launchSettings.json`). Gọi `GET /api/health/db` — trả về
   `"database": "connected"` nghĩa là mọi thứ đã thông.

## Chạy bằng Docker (tuỳ chọn, local)

```bash
cp .env.example .env     # điền connection string / JWT key thật vào .env
docker compose up --build
```

API chạy ở `http://localhost:8080`. Không có service Postgres trong
`docker-compose.yml` vì database dùng Supabase (cloud).

## Khi phát hiện vấn đề schema lúc test (đúng flow migration/code-first)

Sửa trực tiếp trong class ở `PRN232.Domain/Entities/` hoặc `OnModelCreating` của
`PRN232.Infrastructure/Data/AppDbContext.cs`, rồi:

```bash
dotnet ef migrations add <TenMoTaThayDoi> --project PRN232.Infrastructure --startup-project PRN232.Api
dotnet ef database update --project PRN232.Infrastructure --startup-project PRN232.Api
```

Không sửa tay SQL trên Supabase — mọi thay đổi schema đi qua migration để cả nhóm
đồng bộ qua Git.

## Phân module cho từng thành viên

Mỗi module tạo theo đúng khuôn Roles ở trên (DTO → Interface Repository/Service →
Repository/Service → Controller → đăng ký DI trong `PRN232.Api/Program.cs`), làm trên nhánh
riêng, mở PR để merge:

- Auth & User (Roles, Users, IdentityDocuments)
- Property & Room (Property, RoomTypes, Rooms, RoomImages, Amenities, Equipments)
- Booking & Contract (Bookings, Contracts, UtilityReadings)
- Invoice & Payment (Invoices, InvoiceDetails, Payments)
- Maintenance & Notification (MaintenanceRequests, MaintenanceLogs, Notifications) + nối API thật vào FE (PRN232_FE) thay cho mock data

## Lưu ý

- `appsettings.json` chỉ chứa placeholder, không có secret thật — secret thật nằm
  trong User Secrets (local) hoặc biến môi trường (khi deploy lên Railway).
- Entity đặt tên số nhiều trùng tên bảng trong ERD (vd. `Contracts`, `Payments`) để
  khớp 1-1 với thiết kế đã chốt, tránh lệch khi review. Vì vậy primary key phải khai rõ
  bằng `HasKey()` trong `AppDbContext` — xem phần "Gotcha" trong `CODING_STANDARDS.md`.
- 4 cặp FK kép (Users→Contracts, Users→IdentityDocuments, Users→MaintenanceRequests,
  Users→MaintenanceLogs) đã cấu hình `OnDelete(DeleteBehavior.Restrict)` trong
  `AppDbContext.cs` để tránh lỗi multiple cascade paths khi migrate.
- Đã deploy lên Railway: `prn232-production.up.railway.app`. Sau khi restructure sang
  N-layer, nhớ kiểm tra lại build trên Railway (Dockerfile đã cập nhật để publish
  `PRN232.Api/PRN232.Api.csproj`) trước khi merge vào `main`.
