using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
namespace PRN232.Infrastructure.Repositories;
public sealed class BookingApplicationRepository(AppDbContext db, IBookingRepository bookings) : IBookingApplicationRepository
{
    public Task<BookingApplication?> FindAsync(int id,int customerId,CancellationToken token) =>
        db.BookingApplications.Include(a=>a.People).ThenInclude(p=>p.Images)
            .SingleOrDefaultAsync(a=>a.BookingApplicationId==id && a.CustomerId==customerId,token);
    public Task<BookingApplication?> FindDraftAsync(int roomId,int customerId,CancellationToken token) =>
        db.BookingApplications.Include(a=>a.People).ThenInclude(p=>p.Images)
            .Where(a=>a.RoomId==roomId && a.CustomerId==customerId && a.Status=="Draft")
            .OrderByDescending(a=>a.BookingApplicationId).FirstOrDefaultAsync(token);
    public Task<string?> CustomerNameAsync(int customerId,CancellationToken token) =>
        db.Users.Where(u=>u.UserId==customerId && u.IsActive).Select(u=>(string?)u.FullName).SingleOrDefaultAsync(token);
    public async Task<BookingApplication> AddAsync(BookingApplication application,CancellationToken token)
    {
        await using var tx=await db.Database.BeginTransactionAsync(token);
        if(db.Database.IsNpgsql()) {
            // Match booking writes: Room before Customer, including the room FK lock on INSERT.
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Rooms\" WHERE \"RoomId\" = {application.RoomId} FOR UPDATE",token);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Users\" WHERE \"UserId\" = {application.CustomerId} FOR UPDATE",token);
        }
        var existing=await FindDraftAsync(application.RoomId,application.CustomerId,token);
        if(existing is not null)return existing;
        db.Add(application);await SaveAsync(token);await tx.CommitAsync(token);return application;
    }
    public async Task SaveAsync(CancellationToken token)
    {
        await using var tx=db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(token) : null;
        if(db.Database.IsNpgsql()) {
            var ids=db.ChangeTracker.Entries<BookingApplication>()
                .Where(e=>e.State==EntityState.Modified && e.Entity.BookingApplicationId>0)
                .Select(e=>e.Entity.BookingApplicationId).Order().ToArray();
            foreach(var id in ids)
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"BookingApplications\" WHERE \"BookingApplicationId\" = {id} FOR UPDATE",token);
        }
        try {await db.SaveChangesAsync(token);if(tx is not null)await tx.CommitAsync(token);}
        catch(DbUpdateConcurrencyException) {throw new BookingApplicationConflictException("Hồ sơ đã thay đổi. Hãy tải lại trước khi tiếp tục.");}
    }
    public void RemovePerson(BookingApplicationPerson person) {db.RemoveRange(person.Images);db.Remove(person);}
    public async Task<int> SubmitAsync(BookingApplication application,Bookings booking,Func<int,Notifications> notification,
        DateTimeOffset now,IReadOnlyCollection<int> completePersonIds,CancellationToken token)
    {
        await using var tx=await db.Database.BeginTransactionAsync(token);
        // The existing booking writer keeps the Viewing -> Room -> Customer lock order.
        var result=await bookings.AddIfRoomRentableAsync(booking,notification,now);
        if(result.Outcome==BookingAddOutcome.CustomerHasPendingBooking)
        {
            var linked=await db.BookingApplications.AsNoTracking().Where(a=>a.BookingApplicationId==application.BookingApplicationId && a.CustomerId==application.CustomerId)
                .Select(a=>a.BookingId).SingleAsync(token);
            if(linked.HasValue) return linked.Value;
            throw new BookingCustomerHasPendingBookingException(result.PendingBookingId!.Value);
        }
        if(result.Outcome!=BookingAddOutcome.Added) throw new BookingRoomUnavailableException("Phòng không còn trống để đặt cọc.");
        var capacity=await db.Rooms.Where(r=>r.RoomId==booking.RoomId).Select(r=>r.MaxOccupants).SingleAsync(token);
        if(booking.OccupantCount>capacity) throw new BookingApplicationValidationException("Số người vượt sức chứa phòng.");
        application.BookingId=booking.BookingId;application.Status="Submitted";application.SubmittedAtUtc=now.UtcDateTime;
        application.UpdatedAtUtc=now.UtcDateTime;application.Version=Guid.NewGuid();
        foreach(var person in application.People) if(completePersonIds.Contains(person.BookingApplicationPersonId)) {person.Status="PendingReview";person.Version=Guid.NewGuid();}
        await SaveAsync(token);await tx.CommitAsync(token);return booking.BookingId;
    }
}
