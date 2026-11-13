using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Security;

namespace PRN232.Infrastructure.Data;

public class AppDbContext : DbContext, IDataProtectionKeyContext
{
    // Contexts created without DI (unit tests, design-time tooling) still encrypt CCCD numbers,
    // just with a throwaway in-memory key, never falling back to plaintext.
    private static readonly IDataProtectionProvider FallbackDataProtectionProvider =
        new EphemeralDataProtectionProvider();

    private readonly IDataProtectionProvider _dataProtectionProvider;

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : this(options, FallbackDataProtectionProvider) { }

    // DI picks this constructor. EF compiles the model (and with it the CCCD converter's
    // protector) once per process, which is correct because the app has a single singleton
    // IDataProtectionProvider whose key ring lives in the DataProtectionKeys table.
    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        IDataProtectionProvider dataProtectionProvider)
        : base(options)
    {
        _dataProtectionProvider = dataProtectionProvider;
    }

    // Data Protection key ring (A9): persisted in the database so encrypted CCCD numbers stay
    // readable after each Railway redeploy.
    public DbSet<DataProtectionKey> DataProtectionKeys
    {
        get
        {
            return Set<DataProtectionKey>();
        }
    }

    public DbSet<Roles> Roles
    {
        get
        {
            return Set<Roles>();
        }
    }
    public DbSet<Users> Users
    {
        get
        {
            return Set<Users>();
        }
    }
    public DbSet<Property> Properties
    {
        get
        {
            return Set<Property>();
        }
    }
    public DbSet<RoomTypes> RoomTypes
    {
        get
        {
            return Set<RoomTypes>();
        }
    }
    public DbSet<Service> Services
    {
        get
        {
            return Set<Service>();
        }
    }
    public DbSet<RoomTypeServices> RoomTypeServices
    {
        get
        {
            return Set<RoomTypeServices>();
        }
    }
    public DbSet<Rooms> Rooms
    {
        get
        {
            return Set<Rooms>();
        }
    }
    public DbSet<RoomImages> RoomImages
    {
        get
        {
            return Set<RoomImages>();
        }
    }
    public DbSet<Amenities> Amenities
    {
        get
        {
            return Set<Amenities>();
        }
    }
    public DbSet<RoomAmenities> RoomAmenities
    {
        get
        {
            return Set<RoomAmenities>();
        }
    }
    public DbSet<PropertyAmenities> PropertyAmenities
    {
        get
        {
            return Set<PropertyAmenities>();
        }
    }
    public DbSet<PropertyServiceRates> PropertyServiceRates
    {
        get
        {
            return Set<PropertyServiceRates>();
        }
    }
    public DbSet<Equipments> Equipments
    {
        get
        {
            return Set<Equipments>();
        }
    }
    public DbSet<Bookings> Bookings
    {
        get
        {
            return Set<Bookings>();
        }
    }
    public DbSet<Contracts> Contracts
    {
        get
        {
            return Set<Contracts>();
        }
    }
    public DbSet<UtilityReadings> UtilityReadings
    {
        get
        {
            return Set<UtilityReadings>();
        }
    }
    public DbSet<Invoices> Invoices
    {
        get
        {
            return Set<Invoices>();
        }
    }
    public DbSet<InvoiceDetails> InvoiceDetails
    {
        get
        {
            return Set<InvoiceDetails>();
        }
    }
    public DbSet<PaymentRequests> PaymentRequests => Set<PaymentRequests>();
    public DbSet<PaymentWebhookReceipts> PaymentWebhookReceipts => Set<PaymentWebhookReceipts>();
    public DbSet<PaymentReviewCases> PaymentReviewCases => Set<PaymentReviewCases>();
    public DbSet<BookingRefundRequests> BookingRefundRequests => Set<BookingRefundRequests>();
    public DbSet<PayoutAttempts> PayoutAttempts => Set<PayoutAttempts>();
    public DbSet<BookingRefunds> BookingRefunds => Set<BookingRefunds>();
    public DbSet<Payments> Payments
    {
        get
        {
            return Set<Payments>();
        }
    }
    public DbSet<MaintenanceRequests> MaintenanceRequests
    {
        get
        {
            return Set<MaintenanceRequests>();
        }
    }
    public DbSet<MaintenanceLogs> MaintenanceLogs
    {
        get
        {
            return Set<MaintenanceLogs>();
        }
    }
    public DbSet<MaintenanceReports> MaintenanceReports
    {
        get
        {
            return Set<MaintenanceReports>();
        }
    }
    public DbSet<MaintenanceReportImages> MaintenanceReportImages
    {
        get
        {
            return Set<MaintenanceReportImages>();
        }
    }
    public DbSet<Notifications> Notifications
    {
        get
        {
            return Set<Notifications>();
        }
    }
    public DbSet<IdentityDocuments> IdentityDocuments
    {
        get
        {
            return Set<IdentityDocuments>();
        }
    }
    public DbSet<ViewingSchedules> ViewingSchedules
    {
        get
        {
            return Set<ViewingSchedules>();
        }
    }
    public DbSet<InvoiceComplaints> InvoiceComplaints
    {
        get
        {
            return Set<InvoiceComplaints>();
        }
    }
    public DbSet<PropertyManagerAssignment> PropertyManagerAssignments
    {
        get
        {
            return Set<PropertyManagerAssignment>();
        }
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new Configurations.BookingRefundRequestConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.PayoutAttemptConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.BookingRefundConfiguration());
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
        modelBuilder.Entity<PaymentRequests>(entity =>
        {
            entity.HasKey(request => request.PaymentRequestId);
            entity.HasIndex(request => request.OrderCode).IsUnique();
            entity.HasIndex(request => request.PaymentLinkId).IsUnique()
                .HasFilter("\"PaymentLinkId\" IS NOT NULL");
            // An uncertain receipt must be resolved before issuing another checkout.
            entity.HasIndex(request => request.InvoiceId).IsUnique()
                .HasDatabaseName("UX_PaymentRequests_InvoiceId_Active")
                .HasFilter("\"InvoiceId\" IS NOT NULL AND \"Status\" IN ('Creating', 'Pending', 'RequiresReview')");
            entity.HasIndex(request => request.BookingId).IsUnique()
                .HasDatabaseName("UX_PaymentRequests_BookingId_Active")
                .HasFilter("\"BookingId\" IS NOT NULL AND \"Status\" IN ('Creating', 'Pending', 'RequiresReview')");
            entity.ToTable("PaymentRequests", table =>
            {
                table.HasCheckConstraint("CK_PaymentRequests_ExactlyOneTarget",
                    "(\"InvoiceId\" IS NOT NULL AND \"BookingId\" IS NULL) OR (\"InvoiceId\" IS NULL AND \"BookingId\" IS NOT NULL)");
                table.HasCheckConstraint("CK_PaymentRequests_Amount_Positive", "\"Amount\" > 0");
                table.HasCheckConstraint("CK_PaymentRequests_OrderCode_Positive", "\"OrderCode\" > 0");
                table.HasCheckConstraint("CK_PaymentRequests_Currency", "\"Currency\" = 'VND'");
                table.HasCheckConstraint("CK_PaymentRequests_Status",
                    "\"Status\" IN ('Creating', 'Pending', 'Succeeded', 'Cancelled', 'Expired', 'Create-failed', 'RequiresReview')");
                table.HasCheckConstraint("CK_PaymentRequests_UpdatedAt", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
                table.HasCheckConstraint("CK_PaymentRequests_ExpiresAt", "\"ExpiresAtUtc\" IS NULL OR \"ExpiresAtUtc\" > \"CreatedAtUtc\"");
            });
            entity.Property(request => request.Amount).HasPrecision(18, 2);
            entity.Property(request => request.Currency).HasMaxLength(3).IsRequired();
            entity.Property(request => request.Provider).HasMaxLength(32).IsRequired();
            entity.Property(request => request.Status).HasMaxLength(32).IsRequired();
            entity.Property(request => request.PaymentLinkId).HasMaxLength(128);
            entity.Property(request => request.CheckoutUrl).HasMaxLength(2048);
            entity.Property(request => request.ProviderReference).HasMaxLength(256);
            entity.Property(request => request.LastErrorCode).HasMaxLength(128);
            entity.Property(request => request.LastErrorMessage).HasMaxLength(1000);
            entity.HasOne(request => request.Invoice).WithMany(invoice => invoice.PaymentRequests)
                .HasForeignKey(request => request.InvoiceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(request => request.Booking).WithMany(booking => booking.PaymentRequests)
                .HasForeignKey(request => request.BookingId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(request => request.Customer).WithMany(user => user.PaymentRequests)
                .HasForeignKey(request => request.CustomerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(request => request.Payment).WithOne(payment => payment.PaymentRequest)
                .HasForeignKey<PaymentRequests>(request => request.PaymentId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<PaymentWebhookReceipts>(entity =>
        {
            entity.HasKey(receipt => receipt.PaymentWebhookReceiptId);
            entity.Property(receipt => receipt.Source).HasMaxLength(32).HasDefaultValue("Webhook").IsRequired();
            entity.Property(receipt => receipt.Provider).HasMaxLength(32).IsRequired();
            entity.Property(receipt => receipt.Reference).HasMaxLength(256).IsRequired();
            entity.Property(receipt => receipt.PaymentLinkId).HasMaxLength(128).IsRequired();
            entity.Property(receipt => receipt.Amount).HasPrecision(18, 2);
            entity.Property(receipt => receipt.Currency).HasMaxLength(16).IsRequired();
            entity.Property(receipt => receipt.DataCode).HasMaxLength(32).IsRequired();
            entity.Property(receipt => receipt.TransactionDateTime).HasMaxLength(64);
            entity.Property(receipt => receipt.Fingerprint).HasMaxLength(64).IsRequired();
            entity.Property(receipt => receipt.ConflictingFingerprint).HasMaxLength(64);
            entity.Property(receipt => receipt.Outcome).HasMaxLength(32).IsRequired();
            entity.HasIndex(receipt => new { receipt.Provider, receipt.Reference }).IsUnique();
            entity.HasOne(receipt => receipt.PaymentRequest).WithMany()
                .HasForeignKey(receipt => receipt.PaymentRequestId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<PaymentReviewCases>(entity =>
        {
            entity.HasKey(item => item.PaymentReviewCaseId);
            entity.Property(item => item.Reason).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(16).IsRequired();
            entity.Property(item => item.Resolution).HasMaxLength(32);
            entity.Property(item => item.ResolutionNote).HasMaxLength(2000);
            entity.Property(item => item.EvidenceReference).HasMaxLength(256);
            entity.HasIndex(item => item.PaymentWebhookReceiptId).IsUnique();
            entity.HasIndex(item => new { item.Status, item.CreatedAtUtc });
            entity.HasOne(item => item.Receipt).WithMany().HasForeignKey(item => item.PaymentWebhookReceiptId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Invoice).WithMany().HasForeignKey(item => item.InvoiceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ExistingPayment).WithMany().HasForeignKey(item => item.ExistingPaymentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ResolvedByAdmin).WithMany().HasForeignKey(item => item.ResolvedByAdminId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable("PaymentReviewCases", table => table.HasCheckConstraint("CK_PaymentReviewCases_State",
                "(\"Status\" = 'Open' AND \"Resolution\" IS NULL AND \"ResolvedAtUtc\" IS NULL AND \"ResolvedByAdminId\" IS NULL) OR " +
                "(\"Status\" = 'Resolved' AND \"Resolution\" IN ('RefundedExternally', 'NotDuplicate') AND \"ResolutionNote\" IS NOT NULL " +
                "AND \"ResolvedAtUtc\" IS NOT NULL AND \"ResolvedByAdminId\" IS NOT NULL)"));
        });
        modelBuilder.Entity<MaintenanceRequests>().HasKey(x => x.RequestId);
        modelBuilder.Entity<MaintenanceRequests>()
            .Property(request => request.AssignmentNote)
            .HasColumnType("text");
        modelBuilder.Entity<MaintenanceLogs>().HasKey(x => x.LogId);
        modelBuilder.Entity<MaintenanceReports>().HasKey(x => x.ReportId);
        modelBuilder.Entity<MaintenanceReportImages>().HasKey(x => x.ImageId);
        modelBuilder.Entity<Notifications>().HasKey(x => x.NotificationId);
        modelBuilder.Entity<Notifications>()
            .Property(notification => notification.Body)
            .HasColumnType("text");
        modelBuilder.Entity<IdentityDocuments>().HasKey(x => x.IdentityDocumentId);
        modelBuilder.Entity<IdentityDocuments>()
            .Property(document => document.CCCDNumber)
            .HasConversion(CccdProtection.CreateConverter(_dataProtectionProvider));
        modelBuilder.Entity<IdentityDocuments>()
            .HasIndex(document => document.CCCDNumberHash)
            .HasDatabaseName("IX_IdentityDocuments_CCCDNumberHash");
        modelBuilder.Entity<PropertyServiceRates>().HasKey(x => x.RateId);
        modelBuilder.Entity<ViewingSchedules>().HasKey(x => x.ViewingId);
        modelBuilder.Entity<InvoiceComplaints>().HasKey(x => x.ComplaintId);
        modelBuilder.Entity<PropertyManagerAssignment>()
            .HasKey(assignment => new { assignment.PropertyId, assignment.ManagerId });
        modelBuilder.Entity<Invoices>()
            .HasIndex(invoice => new { invoice.ContractId, invoice.InvoiceMonth })
            .IsUnique()
            .HasDatabaseName("UX_Invoices_ContractId_InvoiceMonth");
        modelBuilder.Entity<PropertyManagerAssignment>()
            .HasOne(assignment => assignment.Property)
            .WithMany(property => property.ManagerAssignments)
            .HasForeignKey(assignment => assignment.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PropertyManagerAssignment>()
            .HasOne(assignment => assignment.Manager)
            .WithMany(user => user.ManagerAssignments)
            .HasForeignKey(assignment => assignment.ManagerId)
            .OnDelete(DeleteBehavior.Restrict);

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

        // ---- ViewingSchedules: Restrict để giữ lịch sử khi phòng/người dùng còn được tham chiếu ----
        modelBuilder.Entity<ViewingSchedules>(entity =>
        {
            if (Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                entity.ToTable("ViewingSchedules", table => table.HasCheckConstraint(
                    "CK_ViewingSchedules_ManagerNote_500",
                    "\"ManagerNote\" IS NULL OR char_length(\"ManagerNote\") <= 500"));
            }

            entity.HasOne(v => v.Room).WithMany()
                .HasForeignKey(v => v.RoomId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(v => v.Customer).WithMany()
                .HasForeignKey(v => v.CustomerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(v => v.AssignedManager).WithMany()
                .HasForeignKey(v => v.AssignedTo).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(v => v.RespondedByUser).WithMany()
                .HasForeignKey(v => v.RespondedBy).OnDelete(DeleteBehavior.Restrict);
            entity.Property(v => v.ManagerNote).HasColumnType("text");
            entity.HasIndex(v => v.AssignedTo)
                .HasDatabaseName("IX_ViewingSchedules_AssignedTo");
        });

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
