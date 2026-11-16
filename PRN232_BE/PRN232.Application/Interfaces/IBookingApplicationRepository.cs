using PRN232.Domain.Entities;
using PRN232.Application.ReadModels;
using PRN232.Application.DTOs;
namespace PRN232.Application.Interfaces;
public interface IBookingApplicationRepository
{
    Task<BookingApplication?> FindAsync(int id, int customerId, CancellationToken token);
    Task<BookingApplication?> FindDraftAsync(int roomId, int customerId, CancellationToken token);
    Task<string?> CustomerNameAsync(int customerId, CancellationToken token);
    Task<BookingApplication> AddAsync(BookingApplication application, CancellationToken token);
    Task SaveAsync(CancellationToken token);
    void RemovePerson(BookingApplicationPerson person);
    Task<int> SubmitAsync(BookingApplication application, Bookings booking, Func<int, Notifications> notification,
        DateTimeOffset now, IReadOnlyCollection<int> completePersonIds, CancellationToken token);
    Task<BookingApplicationContext> ContextAsync(BookingApplication application,CancellationToken token);
    Task<BookingApplication?> FindByBookingAsync(int bookingId,int customerId,CancellationToken token);
    Task<BookingApplication?> FindForReviewAsync(int id,int userId,string role,CancellationToken token);
    Task<List<BookingApplication>> ReviewListAsync(int userId,string role,int page,CancellationToken token);
    Task SaveSubmittedAsync(BookingApplication application,int actorId,string role,int? confirmedCount,CancellationToken token);
    Task<BookingApplication?> FindReviewByBookingAsync(int bookingId,int userId,string role,CancellationToken token);
    void AddReview(BookingApplicationReview review);
}
