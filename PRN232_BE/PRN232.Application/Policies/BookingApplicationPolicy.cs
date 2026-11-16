using System.Text.RegularExpressions;
using PRN232.Domain.Entities;

namespace PRN232.Application.Policies;

public static class BookingApplicationPolicy
{
    public static bool HasCompleteIdentity(BookingApplicationPerson person) =>
        !string.IsNullOrWhiteSpace(person.FullName) &&
        Regex.IsMatch(person.IdentityNumber ?? "", "^[0-9]{12}$") &&
        person.ConsentRecordedAtUtc.HasValue &&
        new[] { "Front", "Back" }.All(side => person.Images.Count(image =>
            image.IsCurrent && image.Side == side && image.PersonRevision == person.Revision) == 1);

    // Call only after authorization/room availability checks in the future submission service.
    public static bool CanSubmit(BookingApplication application, int maxOccupants) =>
        application.Status == "Draft" && application.BookingId is null &&
        application.OccupantCount >= 1 && application.OccupantCount <= maxOccupants &&
        application.People.Count == application.OccupantCount &&
        application.People.Select(p => p.Position).Order().SequenceEqual(Enumerable.Range(0, application.OccupantCount)) &&
        HasCompleteIdentity(application.People.Single(p => p.Position == 0));

    public static bool ReadyForContract(BookingApplication application) =>
        application.Status == "Submitted" && application.BookingId.HasValue &&
        application.RosterChangeStatus is not ("Pending" or "Rejected") &&
        application.OccupantCount >= 1 && application.People.Count == application.OccupantCount &&
        application.People.Select(p => p.Position).Order().SequenceEqual(Enumerable.Range(0, application.OccupantCount)) &&
        application.People.All(p => p.Status == "Approved" && HasCompleteIdentity(p));

    public static void InvalidateReview(BookingApplicationPerson person)
    {
        person.Revision++;
        person.Status = "Incomplete";
        person.ReviewedByUserId = null;
        person.ReviewedAtUtc = null;
        person.ReviewNote = null;
        person.Version = Guid.NewGuid();
        // Future edit service must explicitly rebind retained images to the new revision.
    }
}
