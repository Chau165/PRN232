using Microsoft.EntityFrameworkCore;
using PRN232.Domain.Entities;

namespace PRN232.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Roles> Roles => Set<Roles>();
    public DbSet<Users> Users => Set<Users>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<RoomTypes> RoomTypes => Set<RoomTypes>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<RoomTypeServices> RoomTypeServices => Set<RoomTypeServices>();
    public DbSet<Rooms> Rooms => Set<Rooms>();
    public DbSet<RoomImages> RoomImages => Set<RoomImages>();
    public DbSet<Amenities> Amenities => Set<Amenities>();
    public DbSet<RoomAmenities> RoomAmenities => Set<RoomAmenities>();
    public DbSet<PropertyAmenities> PropertyAmenities => Set<PropertyAmenities>();
    public DbSet<PropertyServiceRates> PropertyServiceRates => Set<PropertyServiceRates>();
    public DbSet<Equipments> Equipments => Set<Equipments>();
    public DbSet<Bookings> Bookings => Set<Bookings>();
    public DbSet<Contracts> Contracts => Set<Contracts>();
    public DbSet<UtilityReadings> UtilityReadings => Set<UtilityReadings>();
    public DbSet<Invoices> Invoices => Set<Invoices>();
    public DbSet<InvoiceDetails> InvoiceDetails => Set<InvoiceDetails>();
    public DbSet<Payments> Payments => Set<Payments>();
    public DbSet<MaintenanceRequests> MaintenanceRequests => Set<MaintenanceRequests>();
    public DbSet<MaintenanceLogs> MaintenanceLogs => Set<MaintenanceLogs>();
    public DbSet<MaintenanceReports> MaintenanceReports => Set<MaintenanceReports>();
    public DbSet<MaintenanceReportImages> MaintenanceReportImages => Set<MaintenanceReportImages>();
    public DbSet<Notifications> Notifications => Set<Notifications>();
    public DbSet<IdentityDocuments> IdentityDocuments => Set<IdentityDocuments>();
    public DbSet<ViewingSchedules> ViewingSchedules => Set<ViewingSchedules>();
    public DbSet<InvoiceComplaints> InvoiceComplaints => Set<InvoiceComplaints>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ---- composite keys (junction tables) ----
        modelBuilder.Entity<RoomTypeServices>().HasKey(x => new { x.RoomTypeId, x.ServiceId });
        modelBuilder.Entity<RoomAmenities>().HasKey(x => new { x.RoomId, x.AmenityId });
        modelBuilder.Entity<PropertyAmenities>().HasKey(x => new { x.PropertyId, x.AmenityId });

        // ---- primary keys: tên class để số nhiều (vd. "Amenities") nhưng property
        // khóa chính để số ít (vd. "AmenityId") nên EF Core convention không tự nhận
        // ra được (nó tìm "Id" hoặc "<TênClass>Id" = "AmenitiesId"). Khai rõ hết cho
        // chắc, tránh lỗi "requires a primary key to be defined" lúc add migration ----
        modelBuilder.Entity<Roles>().HasKey(x => x.RoleId);
        modelBuilder.Entity<Users>().HasKey(x => x.UserId);

        // ---- Auth: email là định danh đăng nhập, phải unique ----
        modelBuilder.Entity<Users>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<Property>().HasKey(x => x.PropertyId);
        modelBuilder.Entity<RoomTypes>().HasKey(x => x.RoomTypeId);
        modelBuilder.Entity<Service>().HasKey(x => x.ServiceId);
        modelBuilder.Entity<Rooms>().HasKey(x => x.RoomId);
        modelBuilder.Entity<RoomImages>().HasKey(x => x.RoomImageId);
        modelBuilder.Entity<Amenities>().HasKey(x => x.AmenityId);
        modelBuilder.Entity<Equipments>().HasKey(x => x.EquipmentId);
        modelBuilder.Entity<Bookings>().HasKey(x => x.BookingId);
        modelBuilder.Entity<Contracts>().HasKey(x => x.ContractId);
        modelBuilder.Entity<UtilityReadings>().HasKey(x => x.ReadingId);
        modelBuilder.Entity<Invoices>().HasKey(x => x.InvoiceId);
        modelBuilder.Entity<InvoiceDetails>().HasKey(x => x.InvoiceDetailId);
        modelBuilder.Entity<Payments>().HasKey(x => x.PaymentId);
        modelBuilder.Entity<MaintenanceRequests>().HasKey(x => x.RequestId);
        modelBuilder.Entity<MaintenanceRequests>()
            .Property(request => request.AssignmentNote)
            .HasColumnType("text");
        modelBuilder.Entity<MaintenanceLogs>().HasKey(x => x.LogId);
        modelBuilder.Entity<MaintenanceReports>().HasKey(x => x.ReportId);
        modelBuilder.Entity<MaintenanceReportImages>().HasKey(x => x.ImageId);
        modelBuilder.Entity<Notifications>().HasKey(x => x.NotificationId);
        modelBuilder.Entity<IdentityDocuments>().HasKey(x => x.IdentityDocumentId);
        modelBuilder.Entity<PropertyServiceRates>().HasKey(x => x.RateId);
        modelBuilder.Entity<ViewingSchedules>().HasKey(x => x.ViewingId);
        modelBuilder.Entity<InvoiceComplaints>().HasKey(x => x.ComplaintId);

        // ---- Users: multiple FKs to the same table, set Restrict to avoid
        // multiple-cascade-path errors ----
        modelBuilder.Entity<Contracts>()
            .HasOne(c => c.Customer)
            .WithMany(u => u.ContractsAsCustomer)
            .HasForeignKey(c => c.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Contracts>()
            .HasOne(c => c.ProcessedByStaff)
            .WithMany(u => u.ContractsProcessed)
            .HasForeignKey(c => c.ProcessedByStaffId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<IdentityDocuments>()
            .HasOne(d => d.Customer)
            .WithMany(u => u.IdentityDocumentsOwned)
            .HasForeignKey(d => d.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<IdentityDocuments>()
            .HasOne(d => d.ReviewedByUser)
            .WithMany(u => u.IdentityDocumentsReviewed)
            .HasForeignKey(d => d.ReviewedBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MaintenanceRequests>()
            .HasOne(m => m.AssignedToUser)
            .WithMany(u => u.MaintenanceRequestsAssigned)
            .HasForeignKey(m => m.AssignedTo)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MaintenanceLogs>()
            .HasOne(l => l.ChangedByUser)
            .WithMany(u => u.MaintenanceLogsChanged)
            .HasForeignKey(l => l.ChangedBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MaintenanceReports>()
            .HasOne(report => report.Request)
            .WithOne(request => request.Report)
            .HasForeignKey<MaintenanceReports>(report => report.RequestId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<MaintenanceReports>()
            .HasIndex(report => report.RequestId)
            .IsUnique();
        modelBuilder.Entity<MaintenanceReports>()
            .HasOne(report => report.Reporter)
            .WithMany(user => user.MaintenanceReportsReported)
            .HasForeignKey(report => report.ReportedBy)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MaintenanceReportImages>()
            .HasOne(image => image.Report)
            .WithMany(report => report.Images)
            .HasForeignKey(image => image.ReportId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PropertyAmenities>()
            .HasOne(link => link.Property)
            .WithMany(property => property.PropertyAmenities)
            .HasForeignKey(link => link.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PropertyAmenities>()
            .HasOne(link => link.Amenity)
            .WithMany(amenity => amenity.PropertyAmenities)
            .HasForeignKey(link => link.AmenityId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PropertyServiceRates>(entity =>
        {
            if (Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                entity.ToTable("PropertyServiceRates", table =>
                {
                    table.HasCheckConstraint(
                        "CK_PropertyServiceRates_Price_NonNegative",
                        "\"Price\" >= 0");
                    table.HasCheckConstraint(
                        "CK_PropertyServiceRates_EffectiveFrom_MonthStart",
                        "EXTRACT(DAY FROM \"EffectiveFrom\") = 1");
                    table.HasCheckConstraint(
                        "CK_PropertyServiceRates_EffectiveTo_MonthStart",
                        "\"EffectiveTo\" IS NULL OR EXTRACT(DAY FROM \"EffectiveTo\") = 1");
                    table.HasCheckConstraint(
                        "CK_PropertyServiceRates_EffectiveRange",
                        "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" > \"EffectiveFrom\"");
                    table.HasCheckConstraint(
                        "CK_PropertyServiceRates_UnitBillingBasis",
                        "(\"UnitCode\" = 'kwh' AND \"BillingBasis\" = 'usage') OR " +
                        "(\"UnitCode\" = 'm3' AND \"BillingBasis\" = 'usage') OR " +
                        "(\"UnitCode\" = 'room_month' AND \"BillingBasis\" = 'fixed_room') OR " +
                        "(\"UnitCode\" = 'person_month' AND \"BillingBasis\" = 'occupant') OR " +
                        "(\"UnitCode\" = 'vehicle_month' AND \"BillingBasis\" = 'vehicle')");
                });
            }
            entity.HasIndex(rate => new { rate.PropertyId, rate.ServiceId, rate.EffectiveFrom })
                .HasDatabaseName("IX_PropertyServiceRates_PropertyId_ServiceId_EffectiveFrom");
            entity.HasAlternateKey(rate => new { rate.RateId, rate.ServiceId })
                .HasName("AK_PropertyServiceRates_RateId_ServiceId");
            entity.HasOne(rate => rate.Property)
                .WithMany(property => property.PropertyServiceRates)
                .HasForeignKey(rate => rate.PropertyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(rate => rate.Service)
                .WithMany(service => service.PropertyServiceRates)
                .HasForeignKey(rate => rate.ServiceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InvoiceDetails>()
            .HasOne(detail => detail.Service)
            .WithMany(service => service.InvoiceDetails)
            .HasForeignKey(detail => detail.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<InvoiceDetails>()
            .HasOne(detail => detail.PropertyServiceRate)
            .WithMany(rate => rate.InvoiceDetails)
            .HasForeignKey(detail => new { detail.RateId, detail.ServiceId })
            .HasPrincipalKey(rate => new { rate.RateId, rate.ServiceId })
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<InvoiceDetails>()
            .ToTable("InvoiceDetails", table => table.HasCheckConstraint(
                "CK_InvoiceDetails_RateRequiresService",
                "\"RateId\" IS NULL OR \"ServiceId\" IS NOT NULL"));


        // ---- Bookings <-> Contracts: 1:1 optional (hợp đồng có thể không xuất phát từ booking) ----
        modelBuilder.Entity<Contracts>()
            .HasOne(c => c.Booking)
            .WithOne(b => b.Contract)
            .HasForeignKey<Contracts>(c => c.BookingId)
            .OnDelete(DeleteBehavior.Restrict);

        // ---- Payments: mỗi dòng chỉ gắn với 1 trong Booking/Contract/Invoice/UtilityReading
        // tuỳ loại thanh toán, nên cả 4 FK đều nullable + Restrict ----
        modelBuilder.Entity<Payments>()
            .HasOne(p => p.Booking).WithMany(b => b.Payments)
            .HasForeignKey(p => p.BookingId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Payments>()
            .HasOne(p => p.Contract).WithMany(c => c.Payments)
            .HasForeignKey(p => p.ContractId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Payments>()
            .HasOne(p => p.Invoice).WithMany(i => i.Payments)
            .HasForeignKey(p => p.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Payments>()
            .HasOne(p => p.Reading).WithMany(r => r.Payments)
            .HasForeignKey(p => p.ReadingId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Payments>()
            .HasOne(p => p.User).WithMany(u => u.Payments)
            .HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Restrict);

        // ---- ViewingSchedules: WithMany() không cần collection ngược nên không phải sửa entity
        // Rooms/Users của module khác; Restrict để xoá phòng/người dùng không kéo mất lịch sử hẹn ----
        modelBuilder.Entity<ViewingSchedules>()
            .HasOne(v => v.Room).WithMany()
            .HasForeignKey(v => v.RoomId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ViewingSchedules>()
            .HasOne(v => v.Customer).WithMany()
            .HasForeignKey(v => v.CustomerId).OnDelete(DeleteBehavior.Restrict);

        // ---- InvoiceComplaints: giống ViewingSchedules, không thêm collection ngược vào entity khác;
        // Restrict để không mất lịch sử khiếu nại khi xoá hoá đơn hay người dùng ----
        modelBuilder.Entity<InvoiceComplaints>()
            .HasOne(c => c.Invoice).WithMany()
            .HasForeignKey(c => c.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<InvoiceComplaints>()
            .HasOne(c => c.Customer).WithMany()
            .HasForeignKey(c => c.CustomerId).OnDelete(DeleteBehavior.Restrict);

        // ---- decimal precision (Postgres cần khai precision/scale rõ ràng) ----
        foreach (var property in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetColumnType("decimal(18,2)");
        }

        modelBuilder.Entity<InvoiceDetails>()
            .Property(detail => detail.Quantity)
            .HasColumnType("numeric(18,4)");

        // ---- Toạ độ cần 6 chữ số thập phân (~0,1 m); decimal(18,2) ở vòng lặp trên chỉ chính xác ~1 km ----
        modelBuilder.Entity<Property>().Property(property => property.Latitude).HasColumnType("decimal(9,6)");
        modelBuilder.Entity<Property>().Property(property => property.Longitude).HasColumnType("decimal(9,6)");

        base.OnModelCreating(modelBuilder);
    }
}
