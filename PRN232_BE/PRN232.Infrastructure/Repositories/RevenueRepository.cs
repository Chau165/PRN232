using System.Data;
using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public class RevenueRepository(AppDbContext context) : IRevenueRepository
{
    public Task<int?> GetPropertyOwnerAsync(int propertyId, CancellationToken cancellationToken) =>
        context.Properties.AsNoTracking().Where(property => property.PropertyId == propertyId)
            .Select(property => (int?)property.OwnerId).FirstOrDefaultAsync(cancellationToken);

    public async Task<List<RevenueBucketReadModel>> GetBucketsAsync(int? propertyId, int? ownerId,
        DateTime fromUtc, DateTime untilUtc, CancellationToken cancellationToken)
    {
        if (!context.Database.IsNpgsql())
            return await GetPortableBucketsAsync(propertyId, ownerId, fromUtc, untilUtc, cancellationToken);
        // Aggregate exact numeric amounts in PostgreSQL. Each payment joins one invoice,
        // never its detail lines; include unknown dates independently of the date range.
        var connection = context.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT pr."PropertyId", pr."PropertyName",
                    date_trunc('month', p."PaidAtUtc" AT TIME ZONE 'Asia/Ho_Chi_Minh')::date AS month,
                    COALESCE(SUM(p."Amount"), 0), COUNT(p."PaymentId")
                FROM "Properties" pr
                LEFT JOIN "Rooms" r ON r."PropertyId" = pr."PropertyId"
                LEFT JOIN "Contracts" c ON c."RoomId" = r."RoomId"
                LEFT JOIN "Invoices" i ON i."ContractId" = c."ContractId"
                LEFT JOIN "Payments" p ON p."InvoiceId" = i."InvoiceId" AND p."Status" = 'Success'
                    AND (p."PaidAtUtc" IS NULL OR (p."PaidAtUtc" >= @fromUtc AND p."PaidAtUtc" < @untilUtc))
                WHERE (@propertyId IS NULL OR pr."PropertyId" = @propertyId)
                  AND (@ownerId IS NULL OR pr."OwnerId" = @ownerId)
                GROUP BY pr."PropertyId", pr."PropertyName", month
                ORDER BY pr."PropertyId", month
                """;
            void Parameter(string name, DbType type, object? value)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = name; parameter.DbType = type; parameter.Value = value ?? DBNull.Value;
                command.Parameters.Add(parameter);
            }
            Parameter("propertyId", DbType.Int32, propertyId);
            Parameter("ownerId", DbType.Int32, ownerId);
            Parameter("fromUtc", DbType.DateTime, fromUtc);
            Parameter("untilUtc", DbType.DateTime, untilUtc);
            var result = new List<RevenueBucketReadModel>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                result.Add(new RevenueBucketReadModel(reader.GetInt32(0), reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetFieldValue<DateOnly>(2), reader.GetDecimal(3), checked((int)reader.GetInt64(4))));
            return result;
        }
        finally
        {
            if (opened) await connection.CloseAsync();
        }
    }

    private async Task<List<RevenueBucketReadModel>> GetPortableBucketsAsync(int? propertyId, int? ownerId,
        DateTime fromUtc, DateTime untilUtc, CancellationToken cancellationToken)
    {
        // SQLite test provider cannot aggregate decimal sums or use PostgreSQL time zones.
        var properties = await context.Properties.AsNoTracking()
            .Where(property => propertyId == null || property.PropertyId == propertyId)
            .Where(property => ownerId == null || property.OwnerId == ownerId)
            .Select(property => new { property.PropertyId, property.PropertyName }).ToListAsync(cancellationToken);
        var payments = await context.Payments.AsNoTracking()
            .Where(payment => payment.InvoiceId != null && payment.Status == "Success")
            .Where(payment => propertyId == null || payment.Invoice!.Contract!.Room!.PropertyId == propertyId)
            .Where(payment => ownerId == null || payment.Invoice!.Contract!.Room!.Property!.OwnerId == ownerId)
            .Where(payment => payment.PaidAtUtc == null || (payment.PaidAtUtc >= fromUtc && payment.PaidAtUtc < untilUtc))
            .Select(payment => new { payment.Invoice!.Contract!.Room!.PropertyId, payment.PaidAtUtc, payment.Amount })
            .ToListAsync(cancellationToken);
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        DateOnly? Month(DateTime? instant)
        {
            if (!instant.HasValue) return null;
            var date = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(instant.Value, DateTimeKind.Utc), zone);
            return new DateOnly(date.Year, date.Month, 1);
        }
        var result = payments.GroupBy(payment => new { payment.PropertyId, Month = Month(payment.PaidAtUtc) })
            .Select(group => new RevenueBucketReadModel(group.Key.PropertyId,
                properties.Single(property => property.PropertyId == group.Key.PropertyId).PropertyName,
                group.Key.Month, group.Sum(payment => payment.Amount), group.Count())).ToList();
        foreach (var property in properties.Where(property => !result.Any(bucket => bucket.PropertyId == property.PropertyId)))
            result.Add(new RevenueBucketReadModel(property.PropertyId, property.PropertyName, null, 0, 0));
        return result;
    }
}
