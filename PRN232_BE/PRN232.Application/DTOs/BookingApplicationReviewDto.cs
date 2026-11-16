namespace PRN232.Application.DTOs;
public record ReviewBookingApplicationDto(string Decision, string? Note, Guid Version);
public record BookingApplicationContext(string? BookingStatus,bool HasContract,int PropertyId,string PropertyName,string RoomCode,int Capacity,int? ConfirmedOccupantCount);
