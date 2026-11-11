using System.Text;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
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
using PRN232.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddPayOSConfiguration(builder.Configuration, builder.Environment);

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
// Key mẫu nằm công khai trên GitHub (appsettings.json, .env.example): ai có nó cũng tự ký được
// token Admin. Production mà thiếu key thật thì dừng khởi động luôn, không chạy tiếp với key mẫu.
// Development/Testing được dùng key dev cố định để chạy local và test không cần cấu hình.
var jwtKey = builder.Configuration["Jwt:Key"];
string[] placeholderJwtKeys =
[
    "PUT_A_REAL_SECRET_HERE_VIA_USER_SECRETS_DO_NOT_COMMIT",
    "replace-with-a-random-secret-at-least-32-characters-long",
    "CHANGE_ME_DEV_ONLY_32_CHARACTERS_MIN",
];
var jwtKeyIsUnsafe = string.IsNullOrWhiteSpace(jwtKey) ||
    Encoding.UTF8.GetByteCount(jwtKey) < 32 ||
    placeholderJwtKeys.Contains(jwtKey);
if (jwtKeyIsUnsafe)
{
    if (builder.Environment.IsProduction())
    {
        throw new InvalidOperationException(
            "Jwt:Key chưa được cấu hình hoặc đang dùng key mẫu. " +
            "Đặt biến môi trường Jwt__Key là một chuỗi ngẫu nhiên dài ít nhất 32 ký tự.");
    }
    jwtKey = "DEV_ONLY_LOCAL_SIGNING_KEY_NEVER_USE_IN_PRODUCTION";
}
var jwtSettings = new JwtSettings
{
    Key = jwtKey!,
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
    // Token chỉ hết hạn sau ExpiryMinutes, nên admin khoá tài khoản xong thì token cũ vẫn
    // dùng được. Mỗi request đều kiểm tra lại user còn tồn tại và còn active trong DB.
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            if (!int.TryParse(context.Principal?.FindFirst("sub")?.Value, out var userId) || userId <= 0)
            {
                context.Fail("Token thiếu claim 'sub' hợp lệ.");
                return;
            }

            var users = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
            var user = await users.GetByIdAsync(userId);
            if (user is null || !user.IsActive)
            {
                context.Fail("Tài khoản không tồn tại hoặc đã bị khoá.");
            }
        }
    };
});
builder.Services.AddAuthorization();

// ---- Rate limiting (ASP.NET Core built-in, không cần package ngoài) ----
// Mỗi IP (login) hoặc mỗi user (booking) có hạn mức riêng. Trước đây hạn mức tính chung cho
// cả server: 5 người login trong cùng một phút là người thứ 6 bị chặn.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("LoginPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            ClientIpPartitionKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 5,
                QueueLimit = 0
            }));
    options.AddPolicy("BookingPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.FindFirst("sub")?.Value is { Length: > 0 } userId
                ? $"user:{userId}"
                : ClientIpPartitionKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 10,
                QueueLimit = 0
            }));
});

// Railway đứng sau reverse proxy: không đọc X-Forwarded-For thì mọi request đều có IP của proxy
// và lại dùng chung một hạn mức. ForwardLimit mặc định = 1 nên chỉ lấy IP do proxy gắn vào
// cuối header, client tự thêm IP giả ở đầu header cũng không có tác dụng.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
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
builder.Services.AddScoped<IInvoicePaymentLinkRepository, InvoicePaymentLinkRepository>();
builder.Services.AddScoped<IPaymentRequestStatusRepository, PaymentRequestStatusRepository>();
builder.Services.AddScoped<IPaymentRequestStatusService, PaymentRequestStatusService>();
builder.Services.AddScoped<IPayOSWebhookRepository, PayOSWebhookRepository>();
builder.Services.AddScoped<IPayOSWebhookService, PayOSWebhookService>();
builder.Services.AddScoped<IPaymentReconciliationRepository, PaymentReconciliationRepository>();
builder.Services.AddScoped<IPaymentReconciliationService, PaymentReconciliationService>();
builder.Services.AddSingleton(provider => provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<PayOSSettings>>().Value);
builder.Services.AddHostedService<PRN232.Api.Workers.PaymentReconciliationWorker>();
builder.Services.AddScoped<IInvoicePaymentLinkService>(provider => new InvoicePaymentLinkService(
    provider.GetRequiredService<IInvoicePaymentLinkRepository>(), provider.GetRequiredService<IInvoiceCheckoutGateway>(),
    provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<PayOSSettings>>().Value,
    provider.GetRequiredService<TimeProvider>()));
builder.Services.AddScoped<IReceivablesRepository, ReceivablesRepository>();
builder.Services.AddScoped<IReceivablesService, ReceivablesService>();
builder.Services.AddScoped<IRevenueRepository, RevenueRepository>();
builder.Services.AddScoped<IRevenueService, RevenueService>();
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

app.UseForwardedHeaders();

app.UseSwagger();
app.UseSwaggerUI();

// Local Vite proxies /api to HTTP port 5102. Redirecting that request to
// HTTPS would bypass the proxy and require browser certificate/CORS setup.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors("FrontendPolicy");
// Rate limiter đứng sau UseAuthentication để BookingPolicy đọc được user từ token.
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();

app.Run();

static string ClientIpPartitionKey(HttpContext httpContext)
{
    return $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
}

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
