namespace PRN232.Application.DTOs;

public record AmenityDto(int AmenityId, string Name);

public record CreateAmenityDto(string Name);

public record UpdateAmenityDto(string Name);
