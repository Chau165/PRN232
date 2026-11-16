namespace PRN232.Application.ReadModels;

public record BookingApplicationContext(string? BookingStatus, bool HasContract, int PropertyId,
    string PropertyName, string RoomCode, int Capacity, int? ConfirmedOccupantCount);
