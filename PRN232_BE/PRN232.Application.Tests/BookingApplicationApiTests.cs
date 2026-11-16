using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Validation;
using PRN232.Infrastructure.Data;
using Xunit;
namespace PRN232.Application.Tests;
public class BookingApplicationApiTests
{
    internal sealed class Storage : IBookingIdentityImageStorage
    {
        public int Deleted; public Func<Task>? BeforeUpload;
        public async Task<StoredBookingIdentityImage> UploadAsync(int applicationId,int personId,string side,BookingIdentityUpload image,CancellationToken token=default)
        {
            if(BeforeUpload is not null)await BeforeUpload();
            return new($"applications/{applicationId}/people/{personId}/{side.ToLowerInvariant()}/{Guid.NewGuid():N}.png","image/png",image.Content.Length);
        }
        public Task<string> GetReadUrlAsync(string path,CancellationToken token=default)=>Task.FromResult("https://example.supabase.co/private?token=test");
        public Task DeleteAsync(string path,CancellationToken token=default){Deleted++;return Task.CompletedTask;}
    }
    internal static WebApplicationFactory<PRN232.Api.Controllers.BookingsController> Configure(BookingsApiFactory root,Storage storage)=>root.WithWebHostBuilder(builder=>builder.ConfigureTestServices(services=>{
        services.RemoveAll<IBookingIdentityImageStorage>();services.AddSingleton<IBookingIdentityImageStorage>(storage);
    }));
    internal static async Task<BookingApplicationDto> Draft(HttpClient client,int count=1)
    {
        var response=await client.PostAsJsonAsync("/api/booking-applications",new{roomId=1,moveInDate=BookingRules.Today(TimeProvider.System),occupantCount=count});
        Assert.True(response.IsSuccessStatusCode,await response.Content.ReadAsStringAsync());return (await response.Content.ReadFromJsonAsync<BookingApplicationDto>())!;
    }
    internal static async Task<BookingApplicationDto> Person(HttpClient client,BookingApplicationDto a,int index=0)
    {
        var p=a.People[index];var response=await client.PutAsJsonAsync($"/api/booking-applications/{a.BookingApplicationId}/people/{p.PersonId}",new{fullName=index==0?"Customer A":"Người ở cùng",identityNumber=index==0?"012345678901":"012345678902",consentConfirmed=true,version=a.Version});
        Assert.True(response.IsSuccessStatusCode,await response.Content.ReadAsStringAsync());return (await response.Content.ReadFromJsonAsync<BookingApplicationDto>())!;
    }
    internal static async Task<HttpResponseMessage> Upload(HttpClient client,BookingApplicationDto a,string side,int index=0)
    {
        using var form=new MultipartFormDataContent();var file=new ByteArrayContent(new byte[]{137,80,78,71,13,10,26,10,0,0,0,0});
        file.Headers.ContentType=new("image/png");form.Add(file,"file","test.png");
        return await client.PostAsync($"/api/booking-applications/{a.BookingApplicationId}/people/{a.People[index].PersonId}/images/{side}?version={a.Version}",form);
    }
    internal static async Task<BookingApplicationDto> Complete(HttpClient client,BookingApplicationDto a)
    {
        a=await Person(client,a);
        foreach(var side in new[]{"Front","Back"}){var response=await Upload(client,a,side);Assert.True(response.IsSuccessStatusCode,await response.Content.ReadAsStringAsync());a=(await response.Content.ReadFromJsonAsync<BookingApplicationDto>())!;}
        return a;
    }
    [Fact] public async Task Draft_upload_replace_submit_and_replay_are_atomic_and_do_not_start_hold_early()
    {
        using var root=new BookingsApiFactory();var storage=new Storage();using var factory=Configure(root,storage);using var client=factory.CreateClient();
        await BookingsApiTests.LoginAsync(client,"customer@example.test");var a=await Draft(client);
        using(var scope=factory.Services.CreateScope())Assert.Empty(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Bookings.ToListAsync());
        var missing=await client.PostAsJsonAsync($"/api/booking-applications/{a.BookingApplicationId}/submit",new{version=a.Version});Assert.Equal(HttpStatusCode.BadRequest,missing.StatusCode);
        a=await Complete(client,a);
        var replace=await Upload(client,a,"Front");Assert.True(replace.IsSuccessStatusCode,await replace.Content.ReadAsStringAsync());a=(await replace.Content.ReadFromJsonAsync<BookingApplicationDto>())!;
        var raw=await client.GetStringAsync($"/api/booking-applications/{a.BookingApplicationId}");Assert.DoesNotContain("012345678901",raw);Assert.DoesNotContain("objectPath",raw);
        var first=await client.PostAsJsonAsync($"/api/booking-applications/{a.BookingApplicationId}/submit",new{version=a.Version});Assert.True(first.IsSuccessStatusCode,await first.Content.ReadAsStringAsync());
        var booking=(await first.Content.ReadFromJsonAsync<BookingDto>())!;Assert.Equal("Pending",booking.Status);
        var repeated=await client.PostAsJsonAsync($"/api/booking-applications/{a.BookingApplicationId}/submit",new{version=a.Version});Assert.Equal(booking.BookingId,(await repeated.Content.ReadFromJsonAsync<BookingDto>())!.BookingId);
        using(var scope=factory.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();Assert.Equal(1,await db.Bookings.CountAsync());Assert.Empty(await db.PaymentRequests.ToListAsync());}
        Assert.Equal(HttpStatusCode.Conflict,(await Upload(client,a,"Back")).StatusCode);
    }
    [Fact] public async Task Ownership_stale_version_and_role_checks_protect_identity_data()
    {
        using var root=new BookingsApiFactory();using var factory=Configure(root,new());using var client=factory.CreateClient();
        var swagger=await client.GetAsync("/swagger/v1/swagger.json");
        Assert.True(swagger.IsSuccessStatusCode,await swagger.Content.ReadAsStringAsync());
        await BookingsApiTests.LoginAsync(client,"customer@example.test");var a=await Draft(client);var updated=await Person(client,a);
        var stale=await client.PutAsJsonAsync($"/api/booking-applications/{a.BookingApplicationId}/people/{a.People[0].PersonId}",new{fullName="Other",version=a.Version});Assert.Equal(HttpStatusCode.Conflict,stale.StatusCode);
        await BookingsApiTests.LoginAsync(client,"other@example.test");Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/api/booking-applications/{a.BookingApplicationId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsync($"/api/booking-applications/{a.BookingApplicationId}/people/{a.People[0].PersonId}/images/Front/read-url",null)).StatusCode);
        await BookingsApiTests.LoginAsync(client,"manager@example.test");Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync($"/api/booking-applications/{a.BookingApplicationId}")).StatusCode);
    }
    [Fact] public async Task Upload_losing_concurrency_cleans_the_new_object_without_saving_image()
    {
        using var root=new BookingsApiFactory();var storage=new Storage();using var factory=Configure(root,storage);using var client=factory.CreateClient();
        await BookingsApiTests.LoginAsync(client,"customer@example.test");var a=await Person(client,await Draft(client));
        storage.BeforeUpload=async()=>{using var scope=factory.Services.CreateScope();var nextVersion=Guid.NewGuid();await scope.ServiceProvider.GetRequiredService<AppDbContext>().BookingApplications.Where(x=>x.BookingApplicationId==a.BookingApplicationId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Version,nextVersion));};
        var response=await Upload(client,a,"Front");Assert.Equal(HttpStatusCode.Conflict,response.StatusCode);Assert.Equal(1,storage.Deleted);
        using var check=factory.Services.CreateScope();Assert.Empty(await check.ServiceProvider.GetRequiredService<AppDbContext>().BookingIdentityImages.ToListAsync());
    }
    [Fact] public async Task Unavailable_room_rolls_back_submission_and_keeps_the_draft()
    {
        using var root=new BookingsApiFactory();using var factory=Configure(root,new());using var client=factory.CreateClient();
        await BookingsApiTests.LoginAsync(client,"customer@example.test");var a=await Complete(client,await Draft(client));
        await BookingsApiTests.LoginAsync(client,"other@example.test");var other=await client.PostAsJsonAsync("/api/bookings",new{roomId=1});Assert.Equal(HttpStatusCode.Created,other.StatusCode);
        await BookingsApiTests.LoginAsync(client,"customer@example.test");var response=await client.PostAsJsonAsync($"/api/booking-applications/{a.BookingApplicationId}/submit",new{version=a.Version});Assert.Equal(HttpStatusCode.Conflict,response.StatusCode);
        var current=(await client.GetFromJsonAsync<BookingApplicationDto>($"/api/booking-applications/{a.BookingApplicationId}"))!;Assert.Null(current.BookingId);Assert.Equal("Draft",current.Status);
    }
    [Fact] public async Task Decreasing_people_requires_confirmation_and_removes_only_the_last_person()
    {
        using var root=new BookingsApiFactory();var storage=new Storage();using var factory=Configure(root,storage);using var client=factory.CreateClient();
        await BookingsApiTests.LoginAsync(client,"customer@example.test");
        using(var scope=factory.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var room=await db.Rooms.SingleAsync(r=>r.RoomId==1);room.MaxOccupants=2;await db.SaveChangesAsync();}
        var a=await Draft(client,2);a=await Person(client,a,1);
        var upload=await Upload(client,a,"Front",1);Assert.True(upload.IsSuccessStatusCode,await upload.Content.ReadAsStringAsync());a=(await upload.Content.ReadFromJsonAsync<BookingApplicationDto>())!;
        var declined=await client.PatchAsJsonAsync($"/api/booking-applications/{a.BookingApplicationId}",new{moveInDate=a.MoveInDate,occupantCount=1,version=a.Version});Assert.Equal(HttpStatusCode.Conflict,declined.StatusCode);
        var accepted=await client.PatchAsJsonAsync($"/api/booking-applications/{a.BookingApplicationId}",new{moveInDate=a.MoveInDate,occupantCount=1,version=a.Version,confirmRemovePeople=true});Assert.True(accepted.IsSuccessStatusCode,await accepted.Content.ReadAsStringAsync());
        Assert.Single((await accepted.Content.ReadFromJsonAsync<BookingApplicationDto>())!.People);Assert.Equal(1,storage.Deleted);
    }
    [Fact] public async Task Invalid_viewing_does_not_create_booking_and_submission_can_be_retried()
    {
        using var root=new BookingsApiFactory();using var factory=Configure(root,new());using var client=factory.CreateClient();await BookingsApiTests.LoginAsync(client,"customer@example.test");
        var a=await Complete(client,await Draft(client));
        var invalid=await client.PostAsJsonAsync($"/api/booking-applications/{a.BookingApplicationId}/submit",new{version=a.Version,viewingId=999});Assert.Equal(HttpStatusCode.Conflict,invalid.StatusCode);
        using(var scope=factory.Services.CreateScope())Assert.Empty(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Bookings.ToListAsync());
        var valid=await client.PostAsJsonAsync($"/api/booking-applications/{a.BookingApplicationId}/submit",new{version=a.Version});Assert.True(valid.IsSuccessStatusCode,await valid.Content.ReadAsStringAsync());
    }

}
