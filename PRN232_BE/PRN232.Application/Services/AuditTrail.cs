using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using PRN232.Application.Interfaces;

namespace PRN232.Application.Services;

// Rebuild 5.2: bộ thu gom theo từng request (scoped). Lưu sẵn dạng JSON để trang Admin hiển thị nguyên văn;
// giữ chữ tiếng Việt thay vì \uXXXX cho dễ đọc.
public sealed class AuditTrail : IAuditTrail
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public string? OldValues { get; private set; }
    public string? NewValues { get; private set; }

    public void Record(object? oldValues, object? newValues)
    {
        OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues, Json);
        NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues, Json);
    }
}
