namespace PRN232.Domain.Entities;

// Private preparation data; never serialize these entities directly from an API.
public class BookingApplication
{
    public int BookingApplicationId { get; set; }
    public int CustomerId { get; set; }
    public int RoomId { get; set; }
    public int? BookingId { get; set; }
    public string Status { get; set; } = "Draft";
    public DateOnly MoveInDate { get; set; }
    public int OccupantCount { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
    public ICollection<BookingApplicationPerson> People { get; set; } = new List<BookingApplicationPerson>();
}

public class BookingApplicationPerson
{
    public int BookingApplicationPersonId { get; set; }
    public int BookingApplicationId { get; set; }
    public int Position { get; set; } // 0 = Bạn; subsequent positions = Người ở cùng.
    public string FullName { get; set; } = string.Empty;
    public string IdentityNumber { get; set; } = string.Empty; // encrypted using existing CCCD protection
    public string? IdentityNumberHash { get; set; }
    public string Status { get; set; } = "Incomplete";
    public int Revision { get; set; } = 1;
    public int? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime? ConsentRecordedAtUtc { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
    public BookingApplication Application { get; set; } = null!;
    public ICollection<BookingIdentityImage> Images { get; set; } = new List<BookingIdentityImage>();
}

public class BookingIdentityImage
{
    public int BookingIdentityImageId { get; set; }
    public int BookingApplicationPersonId { get; set; }
    public string Side { get; set; } = string.Empty;
    public string ObjectPath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int PersonRevision { get; set; }
    public bool IsCurrent { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public BookingApplicationPerson Person { get; set; } = null!;
}
