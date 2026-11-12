# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy AS build
WORKDIR /src

COPY PRN232_BE.sln .
COPY PRN232.Domain/PRN232.Domain.csproj PRN232.Domain/
COPY PRN232.Application/PRN232.Application.csproj PRN232.Application/
COPY PRN232.Infrastructure/PRN232.Infrastructure.csproj PRN232.Infrastructure/
COPY PRN232.Api/PRN232.Api.csproj PRN232.Api/
RUN dotnet restore "PRN232.Api/PRN232.Api.csproj"

COPY . .
RUN dotnet publish "PRN232.Api/PRN232.Api.csproj" -c Release -o /app/publish --no-restore

# ---- migration bundle ----
# Đóng gói toàn bộ EF migration thành một file chạy được (efbundle). Railway chạy nó làm
# pre-deploy command trước khi bản mới nhận request, nên DB luôn khớp với code đang deploy.
# Tạo bundle cần dựng host ở design-time: dùng Development để không vướng kiểm tra Jwt:Key
# của Production (A2). Lúc chạy trên Railway thì efbundle đọc biến môi trường thật.
RUN dotnet tool install --global dotnet-ef --version 8.0.10
ENV PATH="${PATH}:/root/.dotnet/tools"
RUN ASPNETCORE_ENVIRONMENT=Development dotnet ef migrations bundle \
      --project PRN232.Infrastructure/PRN232.Infrastructure.csproj \
      --startup-project PRN232.Api/PRN232.Api.csproj \
      --configuration Release --output /app/efbundle --force

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /app/efbundle ./efbundle

# Railway (và hầu hết PaaS) inject biến PORT lúc chạy container, nên bind theo $PORT
# thay vì cố định cổng. Chạy local qua docker-compose thì PORT mặc định 8080.
ENV PORT=8080
EXPOSE 8080
# Không chạy bằng root: image aspnet:8.0 có sẵn user "app" (UID trong biến APP_UID).
# Cổng 8080 > 1024 nên user thường bind được; app chỉ đọc /app, không ghi file.
USER $APP_UID
# CMD (không phải ENTRYPOINT) để lệnh pre-deploy của Railway (./efbundle) thay hẳn lệnh này
# thay vì bị nối thêm vào sau nó.
CMD ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT} dotnet PRN232.Api.dll"]
