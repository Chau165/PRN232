using Microsoft.Extensions.Configuration;
using Npgsql;
using PRN232.Application.DTOs;
using PRN232.Infrastructure.Storage;

var config = new ConfigurationBuilder()
    .SetBasePath(Path.GetFullPath("PRN232_BE/PRN232.Api"))
    .AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddUserSecrets("prn232-be-api-secrets")
    .AddEnvironmentVariables()
    .Build();
using var client = new HttpClient(new StatusHandler()) { Timeout = TimeSpan.FromSeconds(20) };
var storage = new SupabasePropertyImageStorage(client, config);
var failed = false;
if (args.Contains("--db"))
{
    try
    {
        var options = new NpgsqlConnectionStringBuilder(config.GetConnectionString("DefaultConnection")) { Timeout = 10 };
        await using var connection = new NpgsqlConnection(options.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("select current_database()", connection);
        Console.WriteLine($"PASS database={await command.ExecuteScalarAsync()} host={options.Host} port={options.Port}");
    }
    catch (Exception exception)
    {
        Console.WriteLine($"FAIL database {exception.GetType().Name}");
        failed = true;
    }
}
var fixtures = new List<PropertyImageInput>
{
    new("probe.png", "image/png", Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aX1sAAAAASUVORK5CYII="))
};
if (args.Contains("--jpeg"))
    fixtures.Add(new("probe.jpg", "image/jpeg", File.ReadAllBytes("outputs/debug-supabase-upload/probe.jpg")));
if (args.Contains("--webp"))
    fixtures.Add(new("probe.webp", "image/webp", Convert.FromBase64String("UklGRiIAAABXRUJQVlA4IBYAAAAwAQCdASoBAAEADsD+JaQAA3AAAAAA")));
foreach (var fixture in fixtures)
{
    string? url = null;
    try
    {
        url = await storage.UploadAsync(fixture);
        using var read = await client.GetAsync(url);
        read.EnsureSuccessStatusCode();
        if (!(await read.Content.ReadAsByteArrayAsync()).SequenceEqual(fixture.Content))
            throw new InvalidOperationException("Uploaded bytes differ");
        Console.WriteLine($"PASS upload/read {fixture.ContentType}");
    }
    catch (Exception exception)
    {
        Console.WriteLine($"FAIL upload/read {fixture.ContentType}: {exception.GetType().Name}: {exception.Message}");
        failed = true;
    }
    finally
    {
        if (url is not null)
        {
            await storage.DeleteAsync(url);
            Console.WriteLine($"PASS cleanup {fixture.ContentType}");
        }
    }
}
return failed ? 1 : 0;

sealed class StatusHandler : DelegatingHandler
{
    public StatusHandler() : base(new HttpClientHandler()) { }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            Console.WriteLine($"STORAGE HTTP {(int)response.StatusCode} {body[..Math.Min(body.Length, 400)]}");
        }
        return response;
    }
}
