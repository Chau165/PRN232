namespace PRN232.Application.DTOs;

public record ServiceDto(int ServiceId, string Name, decimal Price);

public record CreateServiceDto(string Name, decimal Price);

public record UpdateServiceDto(string Name, decimal Price);
