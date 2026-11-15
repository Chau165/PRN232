using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;

namespace PRN232.Api.Auditing;

// Rebuild 5.1 (PLAN.md D7, lớp 1): ghi MỌI request POST/PUT/PATCH/DELETE vào AuditLogs — ai, endpoint nào,
// đối tượng nào, kết quả (mã HTTP). Ghi ở tầng HTTP nên không bỏ sót các lệnh ExecuteUpdate/ExecuteDelete mà
// EF interceptor không thấy. KHÔNG lưu body request/response (mật khẩu, CCCD, tài khoản ngân hàng).
// Đăng ký ngoài cùng (Order nhỏ nhất) để thấy cả kết quả 400 do FluentValidationFilter trả về.
// Request bị chặn bởi [Authorize] (401/403) không vào tới action filter nên không được ghi ở đây.
public sealed class AuditLogFilter(IAuditLogWriter writer, ICurrentUserService currentUser, ILogger<AuditLogFilter> logger)
    : IAsyncActionFilter
{
    public const int FilterOrder = -1000;
    private static readonly HashSet<string> AuditedMethods = new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH", "DELETE" };

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (!AuditedMethods.Contains(request.Method) ||
            context.ActionDescriptor.EndpointMetadata.OfType<SkipAuditAttribute>().Any())
        {
            await next();
            return;
        }

        var executed = await next();
        var statusCode = executed.Exception is not null && !executed.ExceptionHandled
            ? StatusCodes.Status500InternalServerError
            : executed.Result switch
            {
                IStatusCodeActionResult { StatusCode: { } code } => code,
                _ => StatusCodes.Status200OK
            };
        var controller = context.RouteData.Values["controller"]?.ToString() ?? "Unknown";
        var action = context.RouteData.Values["action"]?.ToString() ?? "Unknown";
        var entry = new AuditLogEntry(
            currentUser.UserId,
            currentUser.Role,
            $"{controller}.{action}",
            request.Method.ToUpperInvariant(),
            request.Path.Value ?? "",
            controller,
            // Tạo mới (POST /api/bookings) chưa có id trên route: lấy id từ CreatedAtAction.
            EntityId(context.RouteData.Values) ??
                (executed.Result is CreatedAtActionResult { RouteValues: { } created } ? EntityId(created) : null),
            statusCode,
            statusCode is >= 200 and < 300,
            context.HttpContext.Connection.RemoteIpAddress?.ToString(),
            context.HttpContext.TraceIdentifier);
        try
        {
            // Không dùng RequestAborted: client ngắt kết nối thì thao tác vẫn có thể đã xong, vẫn phải ghi.
            await writer.WriteAsync(entry, CancellationToken.None);
        }
        catch (Exception exception)
        {
            // Lỗi ghi nhật ký không được làm hỏng thao tác của người dùng.
            logger.LogWarning(exception, "Could not write audit log for {Action} {Path}.", entry.Action, entry.Path);
        }
    }

    // "id" nếu có, không thì tham số route đầu tiên kết thúc bằng "Id" (vd. contractId, bookingId).
    private static string? EntityId(IDictionary<string, object?> values) =>
        values.TryGetValue("id", out var id) && id is not null
            ? id.ToString()
            : values.Where(pair => pair.Key.EndsWith("Id", StringComparison.OrdinalIgnoreCase) && pair.Value is not null)
                .Select(pair => pair.Value!.ToString())
                .FirstOrDefault();
}

// Bỏ qua endpoint POST/PATCH không đổi dữ liệu nghiệp vụ (xem trước, đánh dấu đã đọc) để nhật ký không bị rác.
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class SkipAuditAttribute : Attribute;
