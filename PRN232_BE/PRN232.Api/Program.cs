using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Routing;
using PRN232.Infrastructure.Data;
using PRN232.Application.Interfaces;
using PRN232.Infrastructure.Repositories;
using PRN232.Application.Services;
using PRN232.Application.Settings;
using PRN232.Infrastructure.Security;
using PRN232.Api.Security;
using PRN232.Api.Swagger;
using PRN232.Infrastructure.Storage;
using PRN232.Infrastructure.PaymentGateways;

var builder = WebApplication.CreateBuilder(args);

// ---- Database: Supabase Postgres via Npgsql ----
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ---- CORS: chỉ origin của FE ----
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
if (allowedOrigins is null)
{
    allowedOrigins = new[] { "http://localhost:5173" };
}
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ---- JWT: bind 1 lần thành JwtSettings (POCO, dùng chung cho middleware validate
// token ở dưới và JwtTokenGenerator lúc issue token trong AuthController) ----
var jwtKey = builder.Configuration["Jwt:Key"];
if (jwtKey is null)
{
    jwtKey = "CHANGE_ME_DEV_ONLY_32_CHARACTERS_MIN";
}
var jwtSettings = new JwtSettings
{
    Key = jwtKey,
    Issuer = builder.Configuration["Jwt:Issuer"] ?? "PRN232_BE",
    Audience = builder.Configuration["Jwt:Audience"] ?? "PRN232_FE",
    ExpiryMinutes = builder.Configuration.GetValue<int?>("Jwt:ExpiryMinutes") ?? 120,
};
builder.Services.AddSingleton(jwtSettings);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // Tắt auto-remap claim type (mặc định .NET đổi "sub" -> ClaimTypes.NameIdentifier
    // dạng URI dài, "role" -> ClaimTypes.Role...) để claim trong token giữ nguyên tên
    // ngắn lúc JwtTokenGenerator tạo ra, đọc ở Controller (User.FindFirst("sub")) cho
    // khớp, khỏi nhầm.
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
    };
});
builder.Services.AddAuthorization();

// ---- Rate limiting (ASP.NET Core built-in, không cần package ngoài) ----
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("LoginPolicy", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 5;
        opt.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("BookingPolicy", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 10;
        opt.QueueLimit = 0;
    });
});

// ---- DI: mỗi module thêm repository + service của mình vào đây theo đúng mẫu Roles ----
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IMaintenanceRepository, MaintenanceRepository>();
builder.Services.AddScoped<IMaintenanceService, MaintenanceService>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddHttpClient<IMaintenanceReportImageStorage, SupabaseMaintenanceReportImageStorage>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IIdentityDocumentRepository, IdentityDocumentRepository>();
builder.Services.AddScoped<IIdentityDocumentService, IdentityDocumentService>();
builder.Services.AddScoped<IAmenityRepository, AmenityRepository>();
builder.Services.AddScoped<IAmenityService, AmenityService>();
builder.Services.AddScoped<IServiceRepository, ServiceRepository>();
builder.Services.AddScoped<IServiceService, ServiceCatalogService>();
builder.Services.AddScoped<IPropertyRepository, PropertyRepository>();
builder.Services.AddScoped<IPropertyService, PropertyService>();
builder.Services.AddScoped<IRoomTypeRepository, RoomTypeRepository>();
builder.Services.AddScoped<IRoomTypeService, RoomTypeService>();
builder.Services.AddScoped<IRoomRepository, RoomRepository>();
builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddScoped<IRentalRepository, RentalRepository>();
builder.Services.AddScoped<IRentalService, RentalService>();
builder.Services.AddScoped<IPublicPropertyListingRepository, PublicPropertyListingRepository>();
builder.Services.AddScoped<IPublicPropertyListingService>(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
    var configuredValue = configuration["PublicBrowsing:PropertyDetailEnrichmentEnabled"];
    var enrichmentEnabled = bool.TryParse(configuredValue, out var enabled) && enabled;
    return new PublicPropertyListingService(
        serviceProvider.GetRequiredService<IPublicPropertyListingRepository>(),
        serviceProvider.GetRequiredService<TimeProvider>(),
        enrichmentEnabled);
});
builder.Services.AddScoped<IEquipmentRepository, EquipmentRepository>();
builder.Services.AddScoped<IEquipmentService, EquipmentService>();
builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IPaymentGateway, MockPaymentGateway>();
builder.Services.AddScoped<IViewingScheduleRepository, ViewingScheduleRepository>();
builder.Services.AddScoped<IViewingScheduleService, ViewingScheduleService>();
builder.Services.AddScoped<IContractRepository, ContractRepository>();
builder.Services.AddScoped<IContractService, ContractService>();
builder.Services.AddScoped<IUtilityReadingRepository, UtilityReadingRepository>();
builder.Services.AddScoped<IUtilityReadingService, UtilityReadingService>();
builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IInvoiceComplaintRepository, InvoiceComplaintRepository>();
builder.Services.AddScoped<IInvoiceComplaintService, InvoiceComplaintService>();

builder.Services.AddControllers(options =>
{
    options.Conventions.Add(new RouteTokenTransformerConvention(
        new SlugifyParameterTransformer()));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "PRN232 - Quan Ly Phong Tro API",
        Version = "v1"
    });
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Nhập access token; Swagger tự thêm tiền tố Bearer."
    });
    options.OperationFilter<AuthorizeCheckOperationFilter>();
    options.DocumentFilter<SwaggerTagOrderDocumentFilter>();
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseCors("FrontendPolicy");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

internal sealed class SlugifyParameterTransformer : IOutboundParameterTransformer
{
    public string? TransformOutbound(object? value)
    {
        if (value is null)
        {
            return null;
        }

        return Regex.Replace(
                value.ToString()!,
                "([a-z])([A-Z])",
                "$1-$2",
                RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(100))
            .ToLowerInvariant();
    }
}
