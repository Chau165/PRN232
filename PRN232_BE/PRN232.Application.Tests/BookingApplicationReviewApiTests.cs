using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Application.DTOs;
using PRN232.Infrastructure.Data;
using Xunit;
using static PRN232.Application.Tests.BookingApplicationApiTests;
namespace PRN232.Application.Tests;
public class BookingApplicationReviewApiTests
{
    [Fact] public void Workflow_migration_backfills_existing_rows_safely_and_secures_review_history()
    {
        using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=unused").Options);
        var migrations=db.Database.GetMigrations().ToArray();var index=Array.FindIndex(migrations,m=>m.EndsWith("_AddBookingApplicationReviewWorkflow"));
        var sql=db.GetService<IMigrator>().GenerateScript(migrations[index-1],migrations[index]);
        Assert.Contains("DEFAULT 'None'",sql);
        Assert.Contains("ALTER TABLE public.\"BookingApplicationReviews\" ENABLE ROW LEVEL SECURITY",sql);
        Assert.DoesNotContain("DROP TABLE",sql);Assert.False(db.Database.HasPendingModelChanges());
    }
    internal static async Task<BookingApplicationDto> Deposited(HttpClient client)
    {
        var a=await Complete(client,await Draft(client));
        var submitted=await client.PostAsJsonAsync($"/api/booking-applications/{a.BookingApplicationId}/submit",new{version=a.Version});submitted.EnsureSuccessStatusCode();
        var b=(await submitted.Content.ReadFromJsonAsync<BookingDto>())!;
        var paid=await client.PostAsJsonAsync($"/api/bookings/{b.BookingId}/pay",new{paymentMethod="VNPay"});Assert.True(paid.IsSuccessStatusCode,await paid.Content.ReadAsStringAsync());
        return (await client.GetFromJsonAsync<BookingApplicationDto>($"/api/booking-applications/{a.BookingApplicationId}"))!;
    }
    internal static async Task<BookingApplicationDto> Review(HttpClient client,BookingApplicationDto a,string decision,string? note=null,int index=0)
    {
        var response=await client.PostAsJsonAsync($"/api/booking-application-reviews/{a.BookingApplicationId}/people/{a.People[index].PersonId}/review",new{decision,note,version=a.Version});
        Assert.True(response.IsSuccessStatusCode,await response.Content.ReadAsStringAsync());return (await response.Content.ReadFromJsonAsync<BookingApplicationDto>())!;
    }
    [Fact] public async Task Reject_reason_is_visible_resubmission_invalidates_decision_and_stale_review_is_blocked()
    {
        using var root=new BookingsApiFactory();using var factory=Configure(root,new());using var client=factory.CreateClient();await BookingsApiTests.LoginAsync(client,"customer@example.test");var a=await Deposited(client);
        await BookingsApiTests.LoginAsync(client,"manager@example.test");
        var missing=await client.PostAsJsonAsync($"/api/booking-application-reviews/{a.BookingApplicationId}/people/{a.People[0].PersonId}/review",new{decision="Rejected",version=a.Version});Assert.Equal(HttpStatusCode.BadRequest,missing.StatusCode);
        var rejected=await Review(client,a,"Rejected","Ảnh bị mờ");Assert.Equal("Ảnh bị mờ",rejected.People[0].ReviewNote);
        await BookingsApiTests.LoginAsync(client,"customer@example.test");var own=(await client.GetFromJsonAsync<BookingApplicationDto>($"/api/booking-applications/by-booking/{a.BookingId}"))!;Assert.Equal("Ảnh bị mờ",own.People[0].ReviewNote);
        own=await Person(client,own);Assert.Equal("PendingReview",own.People[0].Status);Assert.Null(own.People[0].ReviewNote);
        await BookingsApiTests.LoginAsync(client,"manager@example.test");var stale=await client.PostAsJsonAsync($"/api/booking-application-reviews/{a.BookingApplicationId}/people/{a.People[0].PersonId}/review",new{decision="Approved",version=a.Version});Assert.Equal(HttpStatusCode.Conflict,stale.StatusCode);
        own=await Review(client,own,"Approved");Assert.Equal("Approved",own.People[0].Status);
        await BookingsApiTests.LoginAsync(client,"customer@example.test");var replace=await Upload(client,own,"Back");Assert.True(replace.IsSuccessStatusCode,await replace.Content.ReadAsStringAsync());own=(await replace.Content.ReadFromJsonAsync<BookingApplicationDto>())!;Assert.Equal("PendingReview",own.People[0].Status);
        using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();Assert.Equal(2,await db.BookingApplicationReviews.CountAsync());Assert.Equal(1,await db.Payments.CountAsync());
    }
    [Fact] public async Task Manager_scope_and_revocation_protect_list_detail_image_and_decisions()
    {
        using var root=new BookingsApiFactory();using var factory=Configure(root,new());using var client=factory.CreateClient();await BookingsApiTests.LoginAsync(client,"customer@example.test");var a=await Deposited(client);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/api/booking-application-reviews")).StatusCode);
        await BookingsApiTests.LoginAsync(client,"manager-b@example.test");Assert.Empty((await client.GetFromJsonAsync<List<BookingApplicationDto>>("/api/booking-application-reviews"))!);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/api/booking-application-reviews/{a.BookingApplicationId}")).StatusCode);
        await BookingsApiTests.LoginAsync(client,"manager@example.test");Assert.Single((await client.GetFromJsonAsync<List<BookingApplicationDto>>("/api/booking-application-reviews"))!);
        using(var scope=factory.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();db.PropertyManagerAssignments.RemoveRange(await db.PropertyManagerAssignments.ToListAsync());await db.SaveChangesAsync();}
        var image=await client.PostAsync($"/api/booking-application-reviews/{a.BookingApplicationId}/people/{a.People[0].PersonId}/images/Front/read-url",null);Assert.Equal(HttpStatusCode.NotFound,image.StatusCode);
        var review=await client.PostAsJsonAsync($"/api/booking-application-reviews/{a.BookingApplicationId}/people/{a.People[0].PersonId}/review",new{decision="Approved",version=a.Version});Assert.Equal(HttpStatusCode.NotFound,review.StatusCode);
        await BookingsApiTests.LoginAsync(client,"admin@example.test");Assert.Single((await client.GetFromJsonAsync<List<BookingApplicationDto>>("/api/booking-application-reviews"))!);
    }
    [Fact] public async Task Roster_changes_remain_proposals_until_manager_approval_and_do_not_change_money()
    {
        using var root=new BookingsApiFactory();using var factory=Configure(root,new());using var client=factory.CreateClient();await BookingsApiTests.LoginAsync(client,"customer@example.test");
        using(var scope=factory.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var room=await db.Rooms.SingleAsync(r=>r.RoomId==1);room.MaxOccupants=2;await db.SaveChangesAsync();}
        var a=await Deposited(client);
        var update=await client.PatchAsJsonAsync($"/api/booking-applications/{a.BookingApplicationId}",new{moveInDate=a.MoveInDate,occupantCount=2,version=a.Version});Assert.True(update.IsSuccessStatusCode,await update.Content.ReadAsStringAsync());a=(await update.Content.ReadFromJsonAsync<BookingApplicationDto>())!;
        Assert.Equal("Pending",a.RosterChangeStatus);Assert.Equal(1,a.ConfirmedOccupantCount);Assert.Equal(2,a.OccupantCount);
        await BookingsApiTests.LoginAsync(client,"manager@example.test");
        var rejected=await client.PostAsJsonAsync($"/api/booking-application-reviews/{a.BookingApplicationId}/roster-review",new{decision="Rejected",note="Cần bổ sung hồ sơ trước",version=a.Version});rejected.EnsureSuccessStatusCode();a=(await rejected.Content.ReadFromJsonAsync<BookingApplicationDto>())!;
        Assert.Equal("Rejected",a.RosterChangeStatus);Assert.Equal(1,a.ConfirmedOccupantCount);
        await BookingsApiTests.LoginAsync(client,"customer@example.test");
        var resend=await client.PostAsJsonAsync($"/api/booking-applications/{a.BookingApplicationId}/roster-resubmit",new{version=a.Version});resend.EnsureSuccessStatusCode();a=(await resend.Content.ReadFromJsonAsync<BookingApplicationDto>())!;Assert.Equal("Pending",a.RosterChangeStatus);
        a=await Person(client,a,1);foreach(var side in new[]{"Front","Back"}){var image=await Upload(client,a,side,1);Assert.True(image.IsSuccessStatusCode,await image.Content.ReadAsStringAsync());a=(await image.Content.ReadFromJsonAsync<BookingApplicationDto>())!;}
        await BookingsApiTests.LoginAsync(client,"manager@example.test");
        a=await Review(client,a,"Approved",index:0);a=await Review(client,a,"Approved",index:1);
        var approve=await client.PostAsJsonAsync($"/api/booking-application-reviews/{a.BookingApplicationId}/roster-review",new{decision="Approved",version=a.Version});Assert.True(approve.IsSuccessStatusCode,await approve.Content.ReadAsStringAsync());a=(await approve.Content.ReadFromJsonAsync<BookingApplicationDto>())!;Assert.Equal(2,a.ConfirmedOccupantCount);
        await BookingsApiTests.LoginAsync(client,"customer@example.test");
        var replace=await client.PutAsJsonAsync($"/api/booking-applications/{a.BookingApplicationId}/people/{a.People[1].PersonId}",new{fullName="Người ở cùng mới",identityNumber="012345678903",consentConfirmed=true,version=a.Version});replace.EnsureSuccessStatusCode();a=(await replace.Content.ReadFromJsonAsync<BookingApplicationDto>())!;
        Assert.Equal("Pending",a.RosterChangeStatus);Assert.False(a.People[1].HasFront);Assert.False(a.People[1].HasBack);Assert.Equal("Incomplete",a.People[1].Status);Assert.Equal(2,a.ConfirmedOccupantCount);
        using var check=factory.Services.CreateScope();var context=check.ServiceProvider.GetRequiredService<AppDbContext>();Assert.Equal(1,await context.Payments.CountAsync());Assert.Equal(3000000m,(await context.Bookings.SingleAsync()).DepositAmount);
    }
    [Fact] public async Task Existing_contract_freezes_customer_updates_and_reviewer_decisions()
    {
        using var root=new BookingsApiFactory();using var factory=Configure(root,new());using var client=factory.CreateClient();await BookingsApiTests.LoginAsync(client,"customer@example.test");var a=await Deposited(client);
        using(var scope=factory.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();db.Contracts.Add(new(){BookingId=a.BookingId,RoomId=1,CustomerId=10,MonthlyRent=3000000m,StartDate=DateOnly.FromDateTime(DateTime.Today),EndDate=DateOnly.FromDateTime(DateTime.Today).AddYears(1),Status="Active"});await db.SaveChangesAsync();}
        var get=(await client.GetFromJsonAsync<BookingApplicationDto>($"/api/booking-applications/{a.BookingApplicationId}"))!;Assert.False(get.CanEdit);
        Assert.Equal(HttpStatusCode.Conflict,(await Upload(client,get,"Front")).StatusCode);
        await BookingsApiTests.LoginAsync(client,"manager@example.test");var review=await client.PostAsJsonAsync($"/api/booking-application-reviews/{a.BookingApplicationId}/people/{a.People[0].PersonId}/review",new{decision="Approved",version=a.Version});Assert.Equal(HttpStatusCode.Conflict,review.StatusCode);
    }
}
