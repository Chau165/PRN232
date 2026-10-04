# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY PRN232_BE.csproj .
RUN dotnet restore "PRN232_BE.csproj"

COPY . .
RUN dotnet publish "PRN232_BE.csproj" -c Release -o /app/publish --no-restore

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Railway (và hầu hết PaaS) inject biến PORT lúc chạy container, nên bind theo $PORT
# thay vì cố định cổng. Chạy local qua docker-compose thì PORT mặc định 8080.
ENV PORT=8080
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT} dotnet PRN232_BE.dll"]
