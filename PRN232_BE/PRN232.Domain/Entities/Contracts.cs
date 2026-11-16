namespace PRN232.Domain.Entities;

public class Contracts
{
    public ICollection<ContractIdentitySnapshot> IdentitySnapshots { get; set; } = new List<ContractIdentitySnapshot>();
    public int ContractId
    {
        get; set;
    }
    public int RoomId
    {
        get; set;
    }
    public int CustomerId
    {
        get; set;
    }
    public int? BookingId
    {
        get; set;
    }
    public int? ProcessedByStaffId
    {
        get; set;
    }
    public DateOnly StartDate
    {
        get; set;
    }
    public DateOnly EndDate
    {
        get; set;
    }
    public decimal MonthlyRent
    {
        get; set;
    }
    public decimal DepositAmount
    {
        get; set;
    }
    // Null only for contracts created before vehicle registration was introduced.
    public int? RegisteredVehicleCount { get; set; }
    // Manual handover deposit is separate from the Booking deposit and may be explicitly recorded as zero.
    public decimal? ManualSecurityDepositAmount { get; set; }
    public DateTime? SecurityDepositAcknowledgedAtUtc { get; set; }
    public int? SecurityDepositRecordedByManagerId { get; set; }
    public Users? SecurityDepositRecordedByManager { get; set; }
    public string Status { get; set; } = string.Empty;

    public decimal? InitialElectricReading { get; set; }
    public decimal? InitialWaterReading { get; set; }
    public DateOnly? MeterHandoverDate { get; set; }
    // Rebuild 3.6a (PLAN D3 bước 6): khách báo ngày dự kiến trả phòng. Trả TRƯỚC ngày kết thúc mà báo trước
    // dưới 30 ngày thì mất phần cọc còn lại khi quyết toán. Mỗi lần đổi ngày tính lại từ lúc báo mới.
    public DateOnly? PlannedMoveOutDate { get; set; }
    public DateTime? MoveOutNoticeAtUtc { get; set; }
    public int? MoveOutNoticeById { get; set; }
    public Users? MoveOutNoticeBy { get; set; }
    // Rebuild 4.2: hợp đồng được tạo do chuyển người đứng tên; DepositAmount là số cọc được chuyển sang.
    public int? TransferredFromContractId { get; set; }
    public Contracts? TransferredFromContract { get; set; }

    public Rooms? Room
    {
        get; set;
    }
    public Users? Customer
    {
        get; set;
    }
    public Bookings? Booking
    {
        get; set;
    }
    public Users? ProcessedByStaff
    {
        get; set;
    }
    public ICollection<UtilityReadings> UtilityReadings { get; set; } = new List<UtilityReadings>();
    public ICollection<Invoices> Invoices { get; set; } = new List<Invoices>();
    public ICollection<Payments> Payments { get; set; } = new List<Payments>();
    public ICollection<MaintenanceRequests> MaintenanceRequests { get; set; } = new List<MaintenanceRequests>();
    public ICollection<ContractOccupants> Occupants { get; set; } = new List<ContractOccupants>();
}
