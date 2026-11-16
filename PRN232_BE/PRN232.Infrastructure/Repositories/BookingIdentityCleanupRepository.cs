using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
namespace PRN232.Infrastructure.Repositories;
public sealed class BookingIdentityCleanupRepository(AppDbContext db, IBookingIdentityImageStorage storage) : IBookingIdentityCleanupRepository
{
    private IQueryable<BookingApplication> Eligible(DateTime cutoff) => db.BookingApplications.Where(a=>a.Status=="Draft" && a.BookingId==null && a.UpdatedAtUtc<cutoff && !db.ContractIdentitySnapshots.Any(s=>s.SourceApplicationId==a.BookingApplicationId));
    public async Task<(int Drafts,int Tasks)> PreviewAsync(DateTime? cutoff,DateTime now,CancellationToken token) =>
        (cutoff.HasValue?await Eligible(cutoff.Value).CountAsync(token):0,await db.BookingIdentityCleanupTasks.CountAsync(t=>t.State=="Pending" || t.State=="Leased",token));
    public async Task EnqueueAsync(string path,DateTime now,CancellationToken token)
    {
        if(!System.Text.RegularExpressions.Regex.IsMatch(path,@"^applications/[1-9][0-9]*/people/[1-9][0-9]*/(front|back)/[a-f0-9]{32}\.(jpg|png|webp)$"))throw new ArgumentException("Invalid cleanup target.");
        var due=now.AddHours(1);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"BookingIdentityCleanupTasks\" (\"ObjectPath\",\"State\",\"Attempts\",\"NextAttemptAtUtc\",\"CreatedAtUtc\") VALUES ({path},'Pending',0,{due},{now}) ON CONFLICT (\"ObjectPath\") DO NOTHING",token);
    }
    public async Task<int> StageAsync(DateTime cutoff,DateTime now,int batch,CancellationToken token)
    {
        var ids=await Eligible(cutoff).OrderBy(a=>a.BookingApplicationId).Select(a=>a.BookingApplicationId).Take(batch).ToArrayAsync(token);var count=0;
        foreach(var id in ids){
            await using var tx=await db.Database.BeginTransactionAsync(token);
            if(db.Database.IsNpgsql())await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"BookingApplications\" WHERE \"BookingApplicationId\"={id} FOR UPDATE",token);
            var version=Guid.NewGuid();
            if(await Eligible(cutoff).Where(a=>a.BookingApplicationId==id).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.Status,"Cancelled").SetProperty(a=>a.Version,version).SetProperty(a=>a.UpdatedAtUtc,now),token)==0)continue;
            var paths=await db.BookingIdentityImages.Where(i=>i.Person.BookingApplicationId==id).Select(i=>i.ObjectPath).ToArrayAsync(token);
            foreach(var path in paths)await EnqueueAsync(path,now,token);
            db.BookingIdentityRetentionAudits.Add(new(){ApplicationId=id,Event="DraftStaged",CreatedAtUtc=now});await db.SaveChangesAsync(token);await tx.CommitAsync(token);count++;
        }return count;
    }
    public async Task<int> ProcessAsync(DateTime now,int batch,CancellationToken token)
    {
        var ids=await db.BookingIdentityCleanupTasks.Where(t=>t.Attempts<10 && t.NextAttemptAtUtc<=now && (t.State=="Pending" || t.State=="Leased" && t.LeaseUntilUtc<now)).OrderBy(t=>t.BookingIdentityCleanupTaskId).Select(t=>t.BookingIdentityCleanupTaskId).Take(batch).ToArrayAsync(token);var count=0;
        foreach(var id in ids){
            var lease=Guid.NewGuid();var until=now.AddMinutes(5);
            if(await db.BookingIdentityCleanupTasks.Where(t=>t.BookingIdentityCleanupTaskId==id && t.Attempts<10 && t.NextAttemptAtUtc<=now && (t.State=="Pending" || t.State=="Leased" && t.LeaseUntilUtc<now)).ExecuteUpdateAsync(s=>s.SetProperty(t=>t.State,"Leased").SetProperty(t=>t.LeaseId,lease).SetProperty(t=>t.LeaseUntilUtc,until).SetProperty(t=>t.Attempts,t=>t.Attempts+1),token)==0)continue;
            await using var tx=await db.Database.BeginTransactionAsync(token);
            var task=await db.BookingIdentityCleanupTasks.AsNoTracking().SingleAsync(t=>t.BookingIdentityCleanupTaskId==id,token);
            if(db.Database.IsNpgsql())await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({task.ObjectPath}))",token);
            var retained=await db.ContractIdentitySnapshots.AnyAsync(s=>s.FrontObjectPath==task.ObjectPath || s.BackObjectPath==task.ObjectPath,token) || await db.BookingIdentityImages.AnyAsync(i=>i.ObjectPath==task.ObjectPath && (i.Person.Application.BookingId!=null || i.Person.Application.Status!="Cancelled"),token);
            var state=retained?"Retained":"Succeeded";string? error=null;
            if(!retained)try {using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));await storage.DeleteAsync(task.ObjectPath,timeout.Token);}catch {state="Pending";error="STORAGE_DELETE_FAILED";}
            var retry=now.AddMinutes(Math.Min(1440,Math.Pow(2,task.Attempts)*5));
            var updated=await db.BookingIdentityCleanupTasks.Where(t=>t.BookingIdentityCleanupTaskId==id && t.LeaseId==lease && t.State=="Leased").ExecuteUpdateAsync(s=>s.SetProperty(t=>t.State,state).SetProperty(t=>t.LastErrorCode,error).SetProperty(t=>t.NextAttemptAtUtc,retry).SetProperty(t=>t.CompletedAtUtc,state=="Pending"?(DateTime?)null:now),token);
            if(updated>0){db.BookingIdentityRetentionAudits.Add(new(){TaskId=id,Event=state=="Pending"?"DeleteRetry":state,CreatedAtUtc=now});await db.SaveChangesAsync(token);count++;}await tx.CommitAsync(token);
        }return count;
    }
    public async Task<int> PurgeAsync(int batch,DateTime now,CancellationToken token)
    {
        var apps=await db.BookingApplications.Include(a=>a.People).ThenInclude(p=>p.Images).Where(a=>a.Status=="Cancelled" && a.BookingId==null && !db.ContractIdentitySnapshots.Any(s=>s.SourceApplicationId==a.BookingApplicationId) && !a.People.Any(p=>p.Images.Any(i=>!db.BookingIdentityCleanupTasks.Any(t=>t.ObjectPath==i.ObjectPath && t.State=="Succeeded")))) .Take(batch).ToListAsync(token);
        foreach(var app in apps){db.RemoveRange(app.People.SelectMany(p=>p.Images));db.RemoveRange(app.People);db.Remove(app);db.BookingIdentityRetentionAudits.Add(new(){ApplicationId=app.BookingApplicationId,Event="DraftPurged",CreatedAtUtc=now});}await db.SaveChangesAsync(token);return apps.Count;
    }
}
