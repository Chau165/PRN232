using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Infrastructure.Data;
using Xunit;

namespace PRN232.Application.Tests;

// CHỈ DÙNG TRONG TEST (đăng ký trong BookingsApiFactory, không có ở API thật).
// Hợp đồng tạo qua API luôn bắt đầu "hôm nay" vì API chặn ngày bắt đầu ở quá khứ. Từ khi tháng đầu tính tiền phòng theo
// số ngày ở (RentProration), hóa đơn tháng này của hợp đồng đó đổi theo ngày chạy test. Các test hóa đơn/thanh toán không
// kiểm tra việc chia ngày gọi endpoint này để hợp đồng bắt đầu từ ngày 1 của tháng → hóa đơn tháng này luôn đủ tháng.
internal sealed class TestContractStartFilter : IStartupFilter
{
    private const string Path = "/__test/contracts/start-at-month-start";

    public static async Task StartAtMonthStartAsync(HttpClient client, int contractId)
    {
        using var response = await client.PostAsync($"{Path}?contractId={contractId}", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            if (context.Request.Path == Path && int.TryParse(context.Request.Query["contractId"], out var contractId))
            {
                var db = context.RequestServices.GetRequiredService<AppDbContext>();
                var contract = await db.Contracts.SingleAsync(item => item.ContractId == contractId);
                contract.StartDate = new DateOnly(contract.StartDate.Year, contract.StartDate.Month, 1);
                await db.SaveChangesAsync();
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return;
            }
            await nextMiddleware();
        });
        next(app);
    };
}
