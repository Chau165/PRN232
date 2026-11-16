using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using PRN232.Infrastructure.Storage;
using Xunit;

namespace PRN232.Application.Tests;

public class BookingIdentityPreparationTests
{
    private static BookingApplication Draft()
    {
        var person = new BookingApplicationPerson {Position=0,FullName="Khách thử",IdentityNumber="012345678901",ConsentRecordedAtUtc=DateTime.UtcNow};
        person.Images.Add(new() {Side="Front",PersonRevision=1});
        person.Images.Add(new() {Side="Back",PersonRevision=1});
        return new() {People=new List<BookingApplicationPerson>{person}};
    }
    [Fact] public void Submission_requires_holder_both_faces_but_allows_incomplete_roommates()
    {
        var app=Draft();Assert.True(BookingApplicationPolicy.CanSubmit(app,2));
        app.OccupantCount=2;app.People.Add(new(){Position=1});
        Assert.True(BookingApplicationPolicy.CanSubmit(app,2));
        Assert.False(BookingApplicationPolicy.CanSubmit(app,1));
        app.People.First().Images.Clear();Assert.False(BookingApplicationPolicy.CanSubmit(app,2));
    }
    [Fact] public void Payment_does_not_approve_identity_and_edits_invalidate_old_images()
    {
        var app=Draft();app.Status="Submitted";app.BookingId=4;
        Assert.False(BookingApplicationPolicy.ReadyForContract(app));
        var person=app.People.Single();person.Status="Approved";
        Assert.True(BookingApplicationPolicy.ReadyForContract(app));
        BookingApplicationPolicy.InvalidateReview(person);
        Assert.False(BookingApplicationPolicy.ReadyForContract(app));
        Assert.False(BookingApplicationPolicy.HasCompleteIdentity(person));
        Assert.Equal(4,app.BookingId);
    }
    [Fact] public void Model_has_private_tables_and_encrypted_identity_converter()
    {
        using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=unused").Options);
        var property=db.Model.FindEntityType(typeof(BookingApplicationPerson))!.FindProperty("IdentityNumber")!;
        var converter=property.GetValueConverter()!;
        Assert.NotEqual("012345678901",converter.ConvertToProvider("012345678901"));
        Assert.False(db.Database.HasPendingModelChanges());
    }
    [Fact] public void Migration_sql_protects_all_identity_tables_and_enforces_current_face_uniqueness()
    {
        using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=unused").Options);
        var migrations=db.Database.GetMigrations().ToArray();
        var sql=db.GetService<IMigrator>().GenerateScript(migrations[^2],migrations[^1]);
        foreach(var table in new[]{"BookingApplications","BookingApplicationPeople","BookingIdentityImages"})
            Assert.Contains($"ALTER TABLE public.\"{table}\" ENABLE ROW LEVEL SECURITY",sql);
        Assert.Contains("pg_roles",sql);
        Assert.Contains("WHERE \"IsCurrent\" = TRUE",sql);
        Assert.DoesNotContain("DROP TABLE",sql);
    }
    private sealed class Handler : HttpMessageHandler
    {
        public bool Public;public int Calls;public string? LastBody;public string? LastPath;
        public string SignedUrl="/object/sign/booking-identities/applications/1/people/2/front/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png?token=test";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            Calls++;LastPath=request.RequestUri!.AbsolutePath;
            LastBody=request.Content is null?null:await request.Content.ReadAsStringAsync(cancellationToken);
            var json=request.Method==HttpMethod.Get ? "{\"public\":"+(Public?"true":"false")+"}"
                :LastPath.Contains("/sign/")?System.Text.Json.JsonSerializer.Serialize(new{signedURL=SignedUrl}):"{}";
            return new(HttpStatusCode.OK){Content=new StringContent(json)};
        }
    }
    private static SupabaseBookingIdentityImageStorage Storage(Handler handler) => new(new HttpClient(handler),
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
            ["Supabase:Url"]="https://example.supabase.co",["Supabase:ServiceRoleKey"]="test-key",["Supabase:BookingIdentityBucket"]="booking-identities"
        }).Build());
    private const string Path="applications/1/people/2/front/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png";
    [Fact] public async Task Public_bucket_is_rejected_before_upload()
    {
        var handler=new Handler{Public=true};
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Storage(handler).UploadAsync(1,2,"Front",Png()));
        Assert.Equal(1,handler.Calls);
    }
    private static BookingIdentityUpload Png() => new(new byte[]{137,80,78,71,13,10,26,10,0,0,0,0},"image/png");
    [Fact] public async Task Upload_returns_object_path_and_signing_has_short_expiry()
    {
        var handler=new Handler();var storage=Storage(handler);
        var uploaded=await storage.UploadAsync(1,2,"Front",Png());
        Assert.StartsWith("applications/1/people/2/front/",uploaded.ObjectPath);
        Assert.DoesNotContain("https",uploaded.ObjectPath);
        var url=await storage.GetReadUrlAsync(Path);
        Assert.StartsWith("https://example.supabase.co/storage/v1/object/sign/",url);
        Assert.Contains("120",handler.LastBody);
    }
    [Theory][InlineData("../other.png")][InlineData("https://evil.test/file")]
    public async Task Foreign_paths_cannot_be_signed_or_deleted(string path)
    {
        var handler=new Handler();var storage=Storage(handler);
        await Assert.ThrowsAsync<ArgumentException>(()=>storage.GetReadUrlAsync(path));
        await Assert.ThrowsAsync<ArgumentException>(()=>storage.DeleteAsync(path));Assert.Equal(0,handler.Calls);
    }
    [Fact] public async Task Mismatched_mime_and_external_signing_response_are_rejected()
    {
        var handler=new Handler();var storage=Storage(handler);
        await Assert.ThrowsAsync<ArgumentException>(()=>storage.UploadAsync(1,2,"Front",new(new byte[20],"image/png")));
        Assert.Equal(0,handler.Calls);
        handler.SignedUrl="https://evil.test/image?token=test";
        await Assert.ThrowsAsync<InvalidOperationException>(()=>storage.GetReadUrlAsync(Path));
    }
}
