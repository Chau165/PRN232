using PRN232.Domain.Entities;
namespace PRN232.Domain.Entities;
public class BookingApplicationReview
{
    public int BookingApplicationReviewId { get; set; }
    public int BookingApplicationId { get; set; }
    // Historical identifiers, intentionally no FK to a person that may later be removed.
    public int? PersonId { get; set; }
    public int? PersonRevision { get; set; }
    public string Kind { get; set; } = "Identity";
    public string Decision { get; set; } = string.Empty;
    public string? Note { get; set; }
    public int ReviewerId { get; set; }
    public DateTime ReviewedAtUtc { get; set; }
    public Guid ExpectedApplicationVersion { get; set; }
}
