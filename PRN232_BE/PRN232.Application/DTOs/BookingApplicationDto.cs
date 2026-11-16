namespace PRN232.Application.DTOs;
public record BookingApplicationDto(int BookingApplicationId, int RoomId, int? BookingId, string Status,
    DateOnly MoveInDate, int OccupantCount, Guid Version, IReadOnlyList<BookingApplicationPersonDto> People, bool DepositAppliesToFirstMonth, string RosterChangeStatus="None", string? RosterChangeNote=null, bool CanEdit=false, int? ConfirmedOccupantCount=null, string? BookingStatus=null, string? PropertyName=null, string? RoomCode=null);
public record BookingApplicationPersonDto(int PersonId, int Position, string FullName, string? MaskedIdentityNumber,
    bool ConsentConfirmed, string Status, bool HasFront, bool HasBack, string? ReviewNote=null, int Revision=1);
public record CreateBookingApplicationDto(int RoomId, DateOnly MoveInDate, int OccupantCount);
public record UpdateBookingApplicationDto(DateOnly MoveInDate, int OccupantCount, Guid Version, bool ConfirmRemovePeople = false);
public record UpdateBookingPersonDto(string FullName, string? IdentityNumber, bool ConsentConfirmed, Guid Version);
public record SubmitBookingApplicationDto(Guid Version, int? ViewingId = null);
