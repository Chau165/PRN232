using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IMaintenanceReportImageStorage
{
    Task<string> UploadAsync(int requestId, MaintenanceReportImageInput image);
    Task<string> GetReadUrlAsync(string objectPath);
    Task DeleteAsync(string objectPath);
}
