using Microsoft.EntityFrameworkCore;
using PRN232.Application.DTOs;
using PRN232.Application.Services;
using PRN232.Domain.Entities;
namespace PRN232.Infrastructure.Repositories;
public sealed partial class BookingApplicationRepository
{
    private readonly Dictionary<int,BookingApplicationContext> reviewContexts = new();
    private IQueryable<BookingApplication> ReviewScope(int actorId,string role)
    {
        var query=db.BookingApplications.Where(a=>a.Status=="Submitted" && a.BookingId!=null &&
            db.Bookings.Any(b=>b.BookingId==a.BookingId && b.Status=="Deposited"));
        if(role=="Admin")return query;
        if(role!="Manager")return query.Where(a=>false);
        return query.Where(a=>db.Rooms.Any(r=>r.RoomId==a.RoomId && db.PropertyManagerAssignments.Any(m=>
            m.PropertyId==r.PropertyId && m.ManagerId==actorId && m.Manager!.IsActive && m.Manager.Role!.RoleName=="Manager")));
    }
    public Task<BookingApplication?> FindReviewByBookingAsync(int bookingId,int userId,string role,CancellationToken token)=>
        ReviewScope(userId,role).Include(a=>a.People).ThenInclude(p=>p.Images).SingleOrDefaultAsync(a=>a.BookingId==bookingId,token);
    public Task<BookingApplication?> FindForReviewAsync(int id,int userId,string role,CancellationToken token)=>
        ReviewScope(userId,role).Include(a=>a.People).ThenInclude(p=>p.Images).SingleOrDefaultAsync(a=>a.BookingApplicationId==id,token);
    public async Task<List<BookingApplication>> ReviewListAsync(int userId,string role,int page,CancellationToken token)
    {
        var rows=await ReviewScope(userId,role).Include(a=>a.People).ThenInclude(p=>p.Images).OrderByDescending(a=>a.UpdatedAtUtc).ThenByDescending(a=>a.BookingApplicationId)
            .Skip((page-1)*50).Take(50).ToListAsync(token);
        var ids=rows.Select(a=>a.BookingApplicationId).ToArray();
        var contexts=await (from a in db.BookingApplications where ids.Contains(a.BookingApplicationId)
            join r in db.Rooms on a.RoomId equals r.RoomId
            join b in db.Bookings on a.BookingId equals (int?)b.BookingId
            select new {a.BookingApplicationId,Context=new BookingApplicationContext(b.Status,db.Contracts.Any(c=>c.BookingId==a.BookingId),
                r.PropertyId,r.Property!.PropertyName,r.RoomCode,r.MaxOccupants,b.OccupantCount)}).ToListAsync(token);
        foreach(var item in contexts)reviewContexts[item.BookingApplicationId]=item.Context;
        return rows;
    }
    public Task<BookingApplication?> FindByBookingAsync(int bookingId,int customerId,CancellationToken token)=>
        db.BookingApplications.Include(a=>a.People).ThenInclude(p=>p.Images).SingleOrDefaultAsync(a=>a.BookingId==bookingId && a.CustomerId==customerId,token);
    public async Task<BookingApplicationContext> ContextAsync(BookingApplication a,CancellationToken token)
    {
        if(reviewContexts.TryGetValue(a.BookingApplicationId,out var cached))return cached;
        var room=await db.Rooms.Where(r=>r.RoomId==a.RoomId).Select(r=>new{r.PropertyId,r.Property!.PropertyName,r.RoomCode,r.MaxOccupants}).SingleAsync(token);
        var booking=a.BookingId.HasValue?await db.Bookings.Where(b=>b.BookingId==a.BookingId).Select(b=>new{b.Status,b.OccupantCount}).SingleOrDefaultAsync(token):null;
        var hasContract=a.BookingId.HasValue && await db.Contracts.AnyAsync(c=>c.BookingId==a.BookingId,token);
        return new(booking?.Status,hasContract,room.PropertyId,room.PropertyName,room.RoomCode,room.MaxOccupants,booking?.OccupantCount);
    }
    public void AddReview(BookingApplicationReview review)=>db.BookingApplicationReviews.Add(review);
    public async Task SaveSubmittedAsync(BookingApplication a,int actorId,string role,int? confirmedCount,CancellationToken token)
    {
        if(!a.BookingId.HasValue)throw new BookingApplicationConflictException("Hồ sơ chưa liên kết booking.");
        await using var tx=await db.Database.BeginTransactionAsync(token);
        if(db.Database.IsNpgsql()) {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Bookings\" WHERE \"BookingId\" = {a.BookingId.Value} FOR UPDATE",token);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Rooms\" WHERE \"RoomId\" = {a.RoomId} FOR UPDATE",token);
            if(role=="Manager")await db.Database.ExecuteSqlInterpolatedAsync($"SELECT m.\"PropertyId\" FROM \"PropertyManagerAssignments\" m JOIN \"Rooms\" r ON r.\"PropertyId\"=m.\"PropertyId\" WHERE r.\"RoomId\"={a.RoomId} AND m.\"ManagerId\"={actorId} FOR SHARE OF m",token);
        }
        var booking=await db.Bookings.SingleOrDefaultAsync(b=>b.BookingId==a.BookingId && b.RoomId==a.RoomId,token);
        if(booking is null || booking.Status!="Deposited" || await db.Contracts.AnyAsync(c=>c.BookingId==a.BookingId,token))
            throw new BookingApplicationConflictException("Booking đã thay đổi hoặc đã có hợp đồng. Không thể sửa hồ sơ chuẩn bị.");
        if(role=="Customer") {
            if(booking.CustomerId!=actorId || a.CustomerId!=actorId)throw new BookingApplicationNotFoundException();
            if(confirmedCount.HasValue)throw new BookingApplicationConflictException("Chỉ Manager xác nhận số người.");
        } else if(!await ReviewScope(actorId,role).AnyAsync(x=>x.BookingApplicationId==a.BookingApplicationId,token))throw new BookingApplicationNotFoundException();
        var capacity=await db.Rooms.Where(r=>r.RoomId==a.RoomId).Select(r=>r.MaxOccupants).SingleAsync(token);
        if(a.OccupantCount>capacity)throw new BookingApplicationConflictException("Số người vượt sức chứa phòng.");
        if(confirmedCount.HasValue){if(role!="Manager")throw new BookingApplicationConflictException("Chỉ Manager xác nhận số người.");booking.OccupantCount=confirmedCount.Value;}
        await SaveAsync(token);await tx.CommitAsync(token);
    }
}
