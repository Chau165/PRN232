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
    public DbSet<Equipments> Equipments => Set<Equipments>();
    public DbSet<Bookings> Bookings => Set<Bookings>();
    public DbSet<Contracts> Contracts => Set<Contracts>();
    public DbSet<UtilityReadings> UtilityReadings => Set<UtilityReadings>();
    public DbSet<Invoices> Invoices => Set<Invoices>();
    public DbSet<InvoiceDetails> InvoiceDetails => Set<InvoiceDetails>();
    public DbSet<Payments> Payments => Set<Payments>();
    public DbSet<MaintenanceRequests> MaintenanceRequests => Set<MaintenanceRequests>();
    public DbSet<MaintenanceLogs> MaintenanceLogs => Set<MaintenanceLogs>();
    public DbSet<Notifications> Notifications => Set<Notifications>();
    public DbSet<IdentityDocuments> IdentityDocuments => Set<IdentityDocuments>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ---- composite keys (junction tables) ----
        modelBuilder.Entity<RoomTypeServices>().HasKey(x => new { x.RoomTypeId, x.ServiceId });
        modelBuilder.Entity<RoomAmenities>().HasKey(x => new { x.RoomId, x.AmenityId });

        // ---- primary keys: tên class để số nhiều (vd. "Amenities") nhưng property
        // khóa chính để số ít (vd. "AmenityId") nên EF Core convention không tự nhận
        // ra được (nó tìm "Id" hoặc "<TênClass>Id" = "AmenitiesId"). Khai rõ hết cho
        // chắc, tránh lỗi "requires a primary key to be defined" lúc add migration ----
        modelBuilder.Entity<Roles>().HasKey(x => x.RoleId);
        modelBuilder.Entity<Users>().HasKey(x => x.UserId);
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
        modelBuilder.Entity<MaintenanceLogs>().HasKey(x => x.LogId);
        modelBuilder.Entity<Notifications>().HasKey(x => x.NotificationId);
        modelBuilder.Entity<IdentityDocuments>().HasKey(x => x.IdentityDocumentId);

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

        // ---- decimal precision (Postgres cần khai precision/scale rõ ràng) ----
        foreach (var property in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetColumnType("decimal(18,2)");
        }

        base.OnModelCreating(modelBuilder);
    }
}
