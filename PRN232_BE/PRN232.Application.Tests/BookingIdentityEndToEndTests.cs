using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Infrastructure.Data;
using PRN232.Infrastructure.Storage;
using Xunit;
using static PRN232.Application.Tests.BookingApplicationApiTests;

namespace PRN232.Application.Tests;

// Real API, SQLite transactions, Storage adapter and payOS signature verifier;
// external services are in-memory HTTP/gateway doubles, never a real merchant or ID card.
public class BookingIdentityEndToEndTests
{
    private sealed class StorageWire : HttpMessageHandler
    {
        internal readonly HashSet<string> Objects = [];
        internal int Signed;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Assert.Equal("identity.test.invalid", request.RequestUri!.Host);
            Assert.Equal("test-storage-key", request.Headers.GetValues("apikey").Single());
            var path = request.RequestUri.AbsolutePath;
            if (request.Method == HttpMethod.Get && path == "/storage/v1/bucket/booking-identities")
                return Json(new { @public = false });
            if (request.Method == HttpMethod.Post && path.StartsWith("/storage/v1/object/sign/"))
            {
                Assert.Contains(path.Replace("/object/sign/", "/object/"), Objects);
                var body = await request.Content!.ReadFromJsonAsync<System.Text.Json.JsonElement>(token);
                Assert.Equal(120, body.GetProperty("expiresIn").GetInt32());
                Signed++;
                return Json(new { signedURL = path["/storage/v1".Length..] + "?token=test-only" });
            }
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.StartsWith("/storage/v1/object/booking-identities/applications/", path);
            Assert.Equal("false", request.Headers.GetValues("x-upsert").Single());
            Assert.Equal("image/png", request.Content!.Headers.ContentType!.MediaType);
            Assert.Equal(new byte[] {137,80,78,71,13,10,26,10,0,0,0,0}, await request.Content.ReadAsByteArrayAsync(token));
            Assert.True(Objects.Add(path));
            return Json(new { });
        }
        private static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
    }

    [Fact]
    public async Task Two_people_upload_pay_review_and_contract_preserve_private_evidence_and_one_payment()
    {
        var wire = new StorageWire(); var gateway = new BookingPayOSApiTests.Gateway();
        using var root = new BookingsApiFactory();
        using var app = root.WithWebHostBuilder(builder =>
        {
            BookingPayOSApiTests.Configure(builder, gateway);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string,string?> {
                ["Supabase:Url"]="https://identity.test.invalid", ["Supabase:ServiceRoleKey"]="test-storage-key",
                ["Supabase:BookingIdentityBucket"]="booking-identities"
            }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IBookingIdentityImageStorage>();
                services.AddSingleton<IBookingIdentityImageStorage>(sp => new SupabaseBookingIdentityImageStorage(new HttpClient(wire), sp.GetRequiredService<IConfiguration>()));
            });
        });
        using var client = app.CreateClient();
        await BookingsApiTests.LoginAsync(client,"customer@example.test");
        using(var scope=app.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Rooms.SingleAsync(r=>r.RoomId==1)).MaxOccupants=2; await db.SaveChangesAsync();
        }
        var identity=await Complete(client,await Draft(client,2));
        var response=await client.PostAsJsonAsync($"/api/booking-applications/{identity.BookingApplicationId}/submit",new{version=identity.Version});
        response.EnsureSuccessStatusCode();var booking=(await response.Content.ReadFromJsonAsync<BookingDto>())!;
        var link=await BookingPayOSApiTests.CreateLinkAsync(client,booking.BookingId);
        var payload=BookingPayOSApiTests.Signed(gateway);
        for(var replay=0;replay<2;replay++)Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/payments/payos/webhook",payload)).StatusCode);
        var status=(await client.GetFromJsonAsync<PaymentRequestStatusDto>($"/api/payment-requests/{link.PaymentRequestId}"))!;
        Assert.Equal("Succeeded",status.Status);Assert.Equal("Deposited",status.BookingStatus);
        identity=(await client.GetFromJsonAsync<BookingApplicationDto>($"/api/booking-applications/by-booking/{booking.BookingId}"))!;
        identity=await Person(client,identity,1);
        foreach(var side in new[]{"Front","Back"})
        {
            var upload=await Upload(client,identity,side,1);upload.EnsureSuccessStatusCode();identity=(await upload.Content.ReadFromJsonAsync<BookingApplicationDto>())!;
        }
        var imageRoute=$"/api/booking-applications/{identity.BookingApplicationId}/people/{identity.People[0].PersonId}/images/Front/read-url";
        await BookingsApiTests.LoginAsync(client,"other@example.test");
        Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsync(imageRoute,null)).StatusCode);Assert.Equal(0,wire.Signed);
        await BookingsApiTests.LoginAsync(client,"manager@example.test");
        identity=await BookingApplicationReviewApiTests.Review(client,identity,"Approved",index:0);
        identity=await BookingApplicationReviewApiTests.Review(client,identity,"Approved",index:1);Assert.True(identity.ReadyForContract);
        var create=await client.PostAsJsonAsync("/api/contracts",new{bookingId=booking.BookingId,startDate=identity.MoveInDate,endDate=identity.MoveInDate.AddYears(1),bookingApplicationVersion=identity.Version,securityDepositHandoverAcknowledged=true});
        Assert.True(create.StatusCode==HttpStatusCode.Created,await create.Content.ReadAsStringAsync());
        var contract=(await create.Content.ReadFromJsonAsync<ContractDto>())!;
        var snapshots=(await client.GetFromJsonAsync<List<ContractIdentitySnapshotDto>>($"/api/contracts/{contract.ContractId}/identity-snapshots"))!;
        Assert.Equal(2,snapshots.Count);
        await BookingsApiTests.LoginAsync(client,"customer@example.test");
        var signed=await client.PostAsync($"/api/contracts/{contract.ContractId}/identity-snapshots/{snapshots[0].SnapshotId}/images/Front/read-url",null);
        Assert.Equal(HttpStatusCode.OK,signed.StatusCode);Assert.Equal(1,wire.Signed);
        using var check=app.Services.CreateScope();var context=check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(4,wire.Objects.Count);Assert.Equal(4,await context.BookingIdentityImages.CountAsync());
        Assert.Single(await context.Payments.ToListAsync());Assert.Equal(2,await context.ContractIdentitySnapshots.CountAsync());
        Assert.Equal("Converted",(await context.BookingApplications.SingleAsync()).Status);
        Assert.NotNull((await context.Payments.SingleAsync()).PaidAtUtc);
    }
}
