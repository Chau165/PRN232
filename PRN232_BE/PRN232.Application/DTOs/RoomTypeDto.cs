namespace PRN232.Application.DTOs;

public record RoomTypeDto(int RoomTypeId, string TypeName, decimal Price);

public record CreateRoomTypeDto(string TypeName, decimal Price);

public record UpdateRoomTypeDto(string TypeName, decimal Price);
