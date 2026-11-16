using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Application.Settings;
using PRN232.Infrastructure.Data;
using Xunit;
using static PRN232.Application.Tests.BookingApplicationApiTests;
using static PRN232.Application.Tests.BookingApplicationReviewApiTests;
namespace PRN232.Application.Tests;
public class ContractIdentityWorkflowTests {
 [Fact]public async Task Reviewed_source_is_versioned_snapshotted_and_authorized(){
  using var root=new BookingsApiFactory();using var factory=Configure(root,new());using var client=factory.CreateClient();await BookingsApiTests.LoginAsync(client,"customer@example.test");var app=await Deposited(client);
  await BookingsApiTests.LoginAsync(client,"manager@example.test");
  var today=app.MoveInDate;
  Task<HttpResponseMessage> Create(Guid version)=>client.PostAsJsonAsync("/api/contracts",new{bookingId=app.BookingId,startDate=today,endDate=today.AddYears(1),bookingApplicationVersion=version,securityDepositHandoverAcknowledged=true});
  Assert.Equal(HttpStatusCode.Conflict,(await Create(app.Version)).StatusCode);
  var old=app.Version;app=await Review(client,app,"Approved");Assert.True(app.ReadyForContract);
  Assert.Equal(HttpStatusCode.Conflict,(await Create(old)).StatusCode);
  var source=await client.GetFromJsonAsync<BookingApplicationDto>($"/api/booking-application-reviews/by-booking/{app.BookingId}/contract-source");Assert.True(source!.ReadyForContract);Assert.DoesNotContain("012345678901",await (await client.GetAsync($"/api/booking-application-reviews/by-booking/{app.BookingId}/contract-source")).Content.ReadAsStringAsync());
  var result=await Create(app.Version);Assert.True(result.StatusCode==HttpStatusCode.Created,await result.Content.ReadAsStringAsync());var contract=(await result.Content.ReadFromJsonAsync<ContractDto>())!;
  var path=$"/api/contracts/{contract.ContractId}/identity-snapshots";
  var rows=(await client.GetFromJsonAsync<List<ContractIdentitySnapshotDto>>(path))!;Assert.Single(rows);Assert.NotEqual("012345678901",rows[0].MaskedIdentityNumber);
  using(var scope=factory.Services.CreateScope()) {var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();Assert.Equal("Converted",(await db.BookingApplications.SingleAsync()).Status);var p=await db.BookingApplicationPeople.SingleAsync();p.FullName="Changed source";await db.SaveChangesAsync();Assert.Equal("Customer A",(await db.ContractIdentitySnapshots.SingleAsync()).FullName);
   var cleanup=scope.ServiceProvider.GetRequiredService<IBookingIdentityCleanupRepository>();var image=(await db.ContractIdentitySnapshots.SingleAsync()).FrontObjectPath;await cleanup.EnqueueAsync(image,DateTime.UtcNow,default);await cleanup.ProcessAsync(DateTime.UtcNow.AddHours(2),20,default);Assert.Equal("Retained",(await db.BookingIdentityCleanupTasks.SingleAsync()).State);}
  await BookingsApiTests.LoginAsync(client,"other@example.test");Assert.False((await client.GetAsync(path)).IsSuccessStatusCode);
  await BookingsApiTests.LoginAsync(client,"customer@example.test");Assert.True((await client.PostAsync($"{path}/{rows[0].SnapshotId}/images/Front/read-url",null)).IsSuccessStatusCode);
 }
 [Fact]public async Task Retention_requires_explicit_policy_retries_and_never_deletes_referenced_objects(){
  using var root=new BookingsApiFactory();var storage=new Storage();using var factory=Configure(root,storage);using var client=factory.CreateClient();await BookingsApiTests.LoginAsync(client,"customer@example.test");var app=await Complete(client,await Draft(client));
  using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var repo=scope.ServiceProvider.GetRequiredService<IBookingIdentityCleanupRepository>();var now=DateTime.UtcNow;
  await db.BookingApplications.Where(a=>a.BookingApplicationId==app.BookingApplicationId).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.UpdatedAtUtc,now.AddDays(-40)));
  var disabled=new BookingIdentityCleanupService(repo,new(){DraftRetentionDays=30},TimeProvider.System);Assert.Equal(1,(await disabled.RunAsync(false,default)).EligibleDrafts);Assert.Equal(0,storage.Deleted);Assert.Equal("Draft",(await db.BookingApplications.AsNoTracking().SingleAsync()).Status);
  var invalid=new BookingIdentityCleanupSettings{Enabled=true};Assert.Throws<InvalidOperationException>(invalid.Validate);
  var dry=new BookingIdentityCleanupService(repo,new(){Enabled=true,DraftRetentionDays=30},TimeProvider.System);await dry.RunAsync(false,default);Assert.Empty(await db.BookingIdentityCleanupTasks.ToListAsync());
  Assert.Equal(1,await repo.StageAsync(now.AddDays(-30),now,20,default));Assert.Equal(0,await repo.ProcessAsync(now,20,default));storage.FailDelete=true;Assert.Equal(2,await repo.ProcessAsync(now.AddHours(2),20,default));Assert.Equal(0,storage.Deleted);Assert.Equal(0,await repo.PurgeAsync(20,now,default));storage.FailDelete=false;Assert.Equal(2,await repo.ProcessAsync(now.AddHours(3),20,default));Assert.Equal(2,storage.Deleted);Assert.Equal(0,await repo.ProcessAsync(now.AddHours(4),20,default));Assert.Equal(1,await repo.PurgeAsync(20,now,default));Assert.Empty(await db.BookingApplications.ToListAsync());
  var live=await Complete(client,await Draft(client));var livePath=await db.BookingIdentityImages.Select(i=>i.ObjectPath).FirstAsync();await repo.EnqueueAsync(livePath,now,default);await repo.EnqueueAsync(livePath,now,default);await repo.ProcessAsync(now.AddHours(2),20,default);Assert.Equal(2,storage.Deleted);Assert.Equal("Retained",(await db.BookingIdentityCleanupTasks.SingleAsync(t=>t.ObjectPath==livePath)).State);
 }
 [Fact]public async Task Unknown_upload_outcome_is_queued_without_exposing_private_object_path(){
  using var root=new BookingsApiFactory();var storage=new Storage();using var factory=Configure(root,storage);using var client=factory.CreateClient();await BookingsApiTests.LoginAsync(client,"customer@example.test");var app=await Person(client,await Draft(client));var path=$"applications/{app.BookingApplicationId}/people/{app.People[0].PersonId}/front/{Guid.NewGuid():N}.png";
  storage.BeforeUpload=()=>throw new BookingIdentityUploadUncertainException(path);var result=await Upload(client,app,"Front");Assert.Equal(HttpStatusCode.BadGateway,result.StatusCode);Assert.DoesNotContain(path,await result.Content.ReadAsStringAsync());
  using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();Assert.Equal(path,(await db.BookingIdentityCleanupTasks.SingleAsync()).ObjectPath);Assert.Empty(await db.BookingIdentityImages.ToListAsync());
 }
 [Fact]public void Migration_adds_private_tables_without_model_drift(){
  using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=unused").Options);var names=db.Database.GetMigrations().ToArray();var i=Array.FindIndex(names,n=>n.EndsWith("_AddContractIdentitySnapshotsAndCleanup"));var sql=db.GetService<IMigrator>().GenerateScript(names[i-1],names[i]);
  foreach(var table in new[]{"ContractIdentitySnapshots","BookingIdentityCleanupTasks","BookingIdentityRetentionAudits"})Assert.Contains($"ALTER TABLE public.\"{table}\" ENABLE ROW LEVEL SECURITY",sql);Assert.False(db.Database.HasPendingModelChanges());
 }
}
