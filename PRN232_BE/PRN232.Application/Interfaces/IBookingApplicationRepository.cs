using PRN232.Domain.Entities;
using PRN232.Application.ReadModels;
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
}
