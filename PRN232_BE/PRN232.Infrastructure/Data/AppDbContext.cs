using PRN232.Application.Validation;
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
    public DbSet<PropertyRoomTypes> PropertyRoomTypes => Set<PropertyRoomTypes>();
    public DbSet<HandoverRecords> HandoverRecords => Set<HandoverRecords>();
    public DbSet<HandoverItems> HandoverItems => Set<HandoverItems>();
    public DbSet<ContractEvidenceFiles> ContractEvidenceFiles => Set<ContractEvidenceFiles>();
    public DbSet<DepositSettlements> DepositSettlements => Set<DepositSettlements>();
    public DbSet<AuditLogs> AuditLogs => Set<AuditLogs>();
    public DbSet<ContractOccupants> ContractOccupants => Set<ContractOccupants>();
    public DbSet<ContractTransfers> ContractTransfers => Set<ContractTransfers>();
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
        // One Google account can be linked to at most one user. NULLs do not collide.
        modelBuilder.Entity<Users>()
            .HasIndex(x => x.GoogleSubject)
            .IsUnique()
            .HasDatabaseName("UX_Users_GoogleSubject");
        modelBuilder.Entity<Property>().HasKey(x => x.PropertyId);
        modelBuilder.Entity<RoomTypes>().HasKey(x => x.RoomTypeId);
        modelBuilder.Entity<PropertyRoomTypes>(entity =>
        {
            entity.HasKey(item => new { item.PropertyId, item.RoomTypeId });
            entity.ToTable("PropertyRoomTypes", table => table.HasCheckConstraint(
                "CK_PropertyRoomTypes_MonthlyRent_Positive",
                "\"MonthlyRent\" > 0"));
            entity.Property(item => item.MonthlyRent).HasPrecision(18, 2);
            entity.Property(item => item.IsActive).HasDefaultValue(true);
            entity.HasIndex(item => new { item.RoomTypeId, item.IsActive })
                .HasDatabaseName("IX_PropertyRoomTypes_RoomTypeId_IsActive");
            entity.HasOne(item => item.Property).WithMany(property => property.PropertyRoomTypes)
                .HasForeignKey(item => item.PropertyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.RoomType).WithMany(roomType => roomType.PropertyRoomTypes)
                .HasForeignKey(item => item.RoomTypeId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Service>().HasKey(x => x.ServiceId);
        modelBuilder.Entity<Rooms>().HasKey(x => x.RoomId);
        modelBuilder.Entity<Rooms>().ToTable("Rooms", table => table.HasCheckConstraint(
            "CK_Rooms_MaxOccupants_Range",
            "\"MaxOccupants\" BETWEEN 1 AND 10"));
        modelBuilder.Entity<Rooms>()
            .HasAlternateKey(room => new { room.PropertyId, room.RoomTypeId, room.RoomId });
        modelBuilder.Entity<Rooms>()
            .HasOne(room => room.PropertyRoomType)
            .WithMany(propertyRoomType => propertyRoomType.Rooms)
            .HasForeignKey(room => new { room.PropertyId, room.RoomTypeId })
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RoomImages>().HasKey(x => x.RoomImageId);
        modelBuilder.Entity<Amenities>().HasKey(x => x.AmenityId);
        modelBuilder.Entity<Equipments>().HasKey(x => x.EquipmentId);
        modelBuilder.Entity<Equipments>(entity =>
        {
            entity.Property(equipment => equipment.CompensationValue).HasPrecision(18, 2);
            entity.ToTable("Equipments", table =>
            {
                table.HasCheckConstraint("CK_Equipments_Quantity_Range", "\"Quantity\" BETWEEN 1 AND 20");
                table.HasCheckConstraint("CK_Equipments_CompensationValue_NonNegative", "\"CompensationValue\" >= 0");
            });
        });
        modelBuilder.Entity<Bookings>().HasKey(x => x.BookingId);
        modelBuilder.Entity<Bookings>(entity =>
        {
            entity.HasIndex(booking => booking.ViewingId)
                .IsUnique()
                .HasDatabaseName("UX_Bookings_ViewingId")
                .HasFilter("\"ViewingId\" IS NOT NULL");
            entity.ToTable("Bookings", table =>
            {
                table.HasCheckConstraint(
                    "CK_Bookings_AgreedMonthlyRent_Positive",
                    "\"AgreedMonthlyRent\" IS NULL OR \"AgreedMonthlyRent\" > 0");
                table.HasCheckConstraint(
                    "CK_Bookings_OccupantCount_Range",
                    "\"OccupantCount\" IS NULL OR \"OccupantCount\" BETWEEN 1 AND 10");
            });
            entity.HasOne(booking => booking.Viewing).WithMany()
                .HasForeignKey(booking => booking.ViewingId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(booking => booking.DepositReturnedBy).WithMany()
                .HasForeignKey(booking => booking.DepositReturnedById).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Contracts>().HasKey(x => x.ContractId);
        modelBuilder.Entity<Contracts>().ToTable("Contracts", table =>
        {
            table.HasCheckConstraint("CK_Contracts_RegisteredVehicleCount_NonNegative",
                "\"RegisteredVehicleCount\" IS NULL OR \"RegisteredVehicleCount\" >= 0");
            table.HasCheckConstraint("CK_Contracts_ManualSecurityDeposit_Recorded",
                "\"ManualSecurityDepositAmount\" IS NULL OR (\"ManualSecurityDepositAmount\" >= 0 AND \"SecurityDepositAcknowledgedAtUtc\" IS NOT NULL AND \"SecurityDepositRecordedByManagerId\" IS NOT NULL)");
        });
        modelBuilder.Entity<Contracts>()
            .Property(contract => contract.ManualSecurityDepositAmount).HasPrecision(18, 2);
        modelBuilder.Entity<Contracts>()
            .HasOne(contract => contract.SecurityDepositRecordedByManager).WithMany()
            .HasForeignKey(contract => contract.SecurityDepositRecordedByManagerId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Contracts>()
            .HasOne(contract => contract.MoveOutNoticeBy).WithMany()
            .HasForeignKey(contract => contract.MoveOutNoticeById).OnDelete(DeleteBehavior.Restrict);
        // Rebuild 3.5: biên bản bàn giao. Không cho xóa hợp đồng/người lập khi còn biên bản (giữ bằng chứng).
        modelBuilder.Entity<HandoverRecords>(entity =>
        {
            entity.HasKey(record => record.HandoverRecordId);
            entity.Property(record => record.Kind).HasMaxLength(20);
            entity.Property(record => record.Note).HasMaxLength(1000);
            entity.HasIndex(record => new { record.ContractId, record.Kind })
                .IsUnique()
                .HasDatabaseName("UX_HandoverRecords_ContractId_Kind");
            entity.HasOne(record => record.Contract).WithMany()
                .HasForeignKey(record => record.ContractId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(record => record.RecordedBy).WithMany()
                .HasForeignKey(record => record.RecordedById).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable("HandoverRecords", table => table.HasCheckConstraint(
                "CK_HandoverRecords_Kind", "\"Kind\" IN ('MoveIn', 'MoveOut')"));
        });
        // Rebuild 4.2: chuyển người đứng tên. Mỗi hợp đồng cũ chỉ sinh ra một hợp đồng mới và chỉ có một
        // đề nghị đang chờ tại một thời điểm.
        modelBuilder.Entity<Contracts>()
            .HasOne(contract => contract.TransferredFromContract).WithMany()
            .HasForeignKey(contract => contract.TransferredFromContractId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Contracts>()
            .HasIndex(contract => contract.TransferredFromContractId).IsUnique()
            .HasDatabaseName("UX_Contracts_TransferredFromContractId")
            .HasFilter("\"TransferredFromContractId\" IS NOT NULL");
        modelBuilder.Entity<ContractTransfers>(entity =>
        {
            entity.HasKey(transfer => transfer.ContractTransferId);
            entity.Property(transfer => transfer.Status).HasMaxLength(20);
            entity.Property(transfer => transfer.CancelReason).HasMaxLength(500);
            entity.HasIndex(transfer => transfer.FromContractId).IsUnique()
                .HasDatabaseName("UX_ContractTransfers_PendingPerContract")
                .HasFilter("\"Status\" = 'Pending'");
            entity.HasIndex(transfer => transfer.NewHolderId);
            entity.HasOne(transfer => transfer.FromContract).WithMany()
                .HasForeignKey(transfer => transfer.FromContractId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(transfer => transfer.NewContract).WithMany()
                .HasForeignKey(transfer => transfer.NewContractId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(transfer => transfer.NewHolder).WithMany()
                .HasForeignKey(transfer => transfer.NewHolderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(transfer => transfer.RequestedBy).WithMany()
                .HasForeignKey(transfer => transfer.RequestedById).OnDelete(DeleteBehavior.Restrict);
            // "Ngày 1 của tháng" do validator kiểm tra (CHECK cần hàm ngày riêng của PostgreSQL, SQLite test không chạy được).
            entity.ToTable("ContractTransfers", table => table.HasCheckConstraint(
                "CK_ContractTransfers_Status", "\"Status\" IN ('Pending', 'Completed', 'Cancelled')"));
        });

        // Rebuild 4.1: người ở cùng. CCCD mã hóa bằng cùng khóa với IdentityDocuments.CCCDNumber.
        modelBuilder.Entity<ContractOccupants>(entity =>
        {
            entity.HasKey(occupant => occupant.ContractOccupantId);
            entity.Property(occupant => occupant.FullName).HasMaxLength(100);
            entity.Property(occupant => occupant.PhoneNumber).HasMaxLength(20);
            entity.Property(occupant => occupant.IdentityNumber)
                .HasConversion(CccdProtection.CreateConverter(_dataProtectionProvider));
            entity.Property(occupant => occupant.Note).HasMaxLength(500);
            entity.HasIndex(occupant => occupant.ContractId);
            entity.HasOne(occupant => occupant.Contract).WithMany(contract => contract.Occupants)
                .HasForeignKey(occupant => occupant.ContractId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(occupant => occupant.AddedBy).WithMany()
                .HasForeignKey(occupant => occupant.AddedById).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable("ContractOccupants", table => table.HasCheckConstraint(
                "CK_ContractOccupants_MoveOutAfterMoveIn", "\"MoveOutDate\" IS NULL OR \"MoveOutDate\" >= \"MoveInDate\""));
        });
        // Rebuild 5.1: nhật ký thao tác. Chỉ mục theo các bộ lọc của trang Admin (mới nhất trước).
        modelBuilder.Entity<AuditLogs>(entity =>
        {
            entity.HasKey(log => log.AuditLogId);
            entity.Property(log => log.Role).HasMaxLength(50);
            entity.Property(log => log.Action).HasMaxLength(150);
            entity.Property(log => log.HttpMethod).HasMaxLength(10);
            entity.Property(log => log.Path).HasMaxLength(300);
            entity.Property(log => log.EntityType).HasMaxLength(100);
            entity.Property(log => log.EntityId).HasMaxLength(50);
            entity.Property(log => log.IpAddress).HasMaxLength(64);
            entity.Property(log => log.CorrelationId).HasMaxLength(100);
            entity.HasIndex(log => log.CreatedAtUtc);
            entity.HasIndex(log => new { log.UserId, log.CreatedAtUtc });
            entity.HasIndex(log => new { log.EntityType, log.EntityId });
        });
        modelBuilder.Entity<DepositSettlements>(entity =>
        {
            entity.HasKey(settlement => settlement.DepositSettlementId);
            entity.HasIndex(settlement => settlement.ContractId).IsUnique()
                .HasDatabaseName("UX_DepositSettlements_ContractId");
            foreach (var amount in new[] { "DepositAmount", "DamageCharges", "OutstandingDebt", "RetainedForDebt",
                         "ForfeitedAmount", "RefundAmount", "AmountDue" })
            {
                entity.Property<decimal>(amount).HasPrecision(18, 2);
            }
            entity.Property(settlement => settlement.Note).HasMaxLength(1000);
            entity.HasOne(settlement => settlement.Contract).WithMany()
                .HasForeignKey(settlement => settlement.ContractId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(settlement => settlement.SettledBy).WithMany()
                .HasForeignKey(settlement => settlement.SettledById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(settlement => settlement.RefundConfirmedBy).WithMany()
                .HasForeignKey(settlement => settlement.RefundConfirmedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(settlement => settlement.DueCollectedBy).WithMany()
                .HasForeignKey(settlement => settlement.DueCollectedById).OnDelete(DeleteBehavior.Restrict);
            // Mọi đồng cọc đều có chỗ đi: bù bồi thường + giữ bù nợ + mất + hoàn = cọc.
            entity.ToTable("DepositSettlements", table =>
            {
                table.HasCheckConstraint("CK_DepositSettlements_NonNegative",
                    "\"DepositAmount\" >= 0 AND \"DamageCharges\" >= 0 AND \"OutstandingDebt\" >= 0 AND \"RetainedForDebt\" >= 0 AND \"ForfeitedAmount\" >= 0 AND \"RefundAmount\" >= 0 AND \"AmountDue\" >= 0");
                table.HasCheckConstraint("CK_DepositSettlements_Balanced",
                    "\"DamageCharges\" - \"AmountDue\" + \"RetainedForDebt\" + \"ForfeitedAmount\" + \"RefundAmount\" = \"DepositAmount\"");
            });
        });
        modelBuilder.Entity<ContractEvidenceFiles>(entity =>
        {
            entity.HasKey(file => file.ContractEvidenceFileId);
            entity.Property(file => file.Kind).HasMaxLength(20);
            entity.Property(file => file.ObjectPath).HasMaxLength(300);
            entity.Property(file => file.ContentType).HasMaxLength(50);
            entity.Property(file => file.FileName).HasMaxLength(255);
            entity.Property(file => file.HiddenReason).HasMaxLength(500);
            entity.HasIndex(file => new { file.ContractId, file.Kind });
            entity.HasIndex(file => file.ObjectPath).IsUnique();
            entity.HasOne(file => file.Contract).WithMany()
                .HasForeignKey(file => file.ContractId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(file => file.UploadedBy).WithMany()
                .HasForeignKey(file => file.UploadedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(file => file.HiddenBy).WithMany()
                .HasForeignKey(file => file.HiddenById).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable("ContractEvidenceFiles", table => table.HasCheckConstraint(
                "CK_ContractEvidenceFiles_Kind", "\"Kind\" IN ('MoveIn', 'MoveOut', 'SignedContract')"));
        });
        modelBuilder.Entity<HandoverItems>(entity =>
        {
            entity.HasKey(item => item.HandoverItemId);
            entity.Property(item => item.EquipmentName).HasMaxLength(200);
            entity.Property(item => item.Condition).HasMaxLength(20);
            entity.Property(item => item.Note).HasMaxLength(500);
            entity.Property(item => item.CompensationValue).HasPrecision(18, 2);
            entity.Property(item => item.ChargeAmount).HasPrecision(18, 2);
            entity.HasOne(item => item.HandoverRecord).WithMany(record => record.Items)
                .HasForeignKey(item => item.HandoverRecordId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Equipment).WithMany()
                .HasForeignKey(item => item.EquipmentId).OnDelete(DeleteBehavior.SetNull);
            entity.ToTable("HandoverItems", table =>
            {
                table.HasCheckConstraint("CK_HandoverItems_Condition",
                    "\"Condition\" IN ('Good', 'Worn', 'Damaged', 'Missing')");
                table.HasCheckConstraint("CK_HandoverItems_Quantity_Range", "\"Quantity\" BETWEEN 1 AND 20");
                table.HasCheckConstraint("CK_HandoverItems_Charge",
                    "\"ChargeAmount\" >= 0 AND \"ChargeAmount\" <= \"CompensationValue\" * \"Quantity\"");
            });
        });
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
            .Property(document => document.ReviewNote)
            .HasMaxLength(IdentityDocumentReviewRules.NoteMaxLength);
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
                        "(\"UnitCode\" = 'person_month' AND \"BillingBasis\" = 'additional_occupant') OR " +
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

            entity.HasIndex(viewing => new { viewing.PropertyId, viewing.RoomTypeId })
                .HasDatabaseName("IX_ViewingSchedules_PropertyId_RoomTypeId");
            entity.HasOne(v => v.Property).WithMany()
                .HasForeignKey(v => v.PropertyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(v => v.RoomType).WithMany()
                .HasForeignKey(v => v.RoomTypeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(v => v.Room).WithMany(room => room.ViewingSchedules)
                .HasForeignKey(v => new { v.PropertyId, v.RoomTypeId, v.RoomId })
                .HasPrincipalKey(room => new { room.PropertyId, room.RoomTypeId, room.RoomId })
                .OnDelete(DeleteBehavior.Restrict);
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

        modelBuilder.Entity<Contracts>().Property(contract => contract.InitialElectricReading).HasColumnType("numeric");
        modelBuilder.Entity<Contracts>().Property(contract => contract.InitialWaterReading).HasColumnType("numeric");

        modelBuilder.Entity<InvoiceDetails>()
            .Property(detail => detail.Quantity)
            .HasColumnType("numeric(18,4)");

        // ---- Toạ độ cần 6 chữ số thập phân (~0,1 m); decimal(18,2) ở vòng lặp trên chỉ chính xác ~1 km ----
        modelBuilder.Entity<Property>().Property(property => property.Latitude).HasColumnType("decimal(9,6)");
        modelBuilder.Entity<Property>().Property(property => property.Longitude).HasColumnType("decimal(9,6)");

        base.OnModelCreating(modelBuilder);
    }
}
