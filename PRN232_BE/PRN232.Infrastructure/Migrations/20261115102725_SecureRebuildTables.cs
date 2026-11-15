using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN232.Infrastructure.Migrations
{
    // Rebuild: các bảng mới của đợt rebuild được tạo mà chưa bật Row Level Security. Trên Supabase, bảng ở schema
    // public không bật RLS thì ai có anon key cũng đọc/ghi được qua Data API (vd. họ tên, SĐT người ở cùng, IP trong
    // nhật ký). Các bảng này chỉ do API .NET dùng (kết nối bằng role chủ bảng nên không bị RLS chặn), nên bật RLS,
    // không tạo policy nào, và thu hồi quyền của các role API — giống SecurePaymentTables.
    /// <inheritdoc />
    public partial class SecureRebuildTables : Migration
    {
        internal static readonly string[] Tables =
        [
            "HandoverRecords", "HandoverItems", "ContractEvidenceFiles", "DepositSettlements",
            "AuditLogs", "ContractOccupants", "ContractTransfers"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.Sql($"""
                    ALTER TABLE public."{table}" ENABLE ROW LEVEL SECURITY;
                    REVOKE ALL PRIVILEGES ON TABLE public."{table}" FROM PUBLIC;
                    DO $rebuild_security$
                    DECLARE
                        api_role text;
                    BEGIN
                        FOREACH api_role IN ARRAY ARRAY['anon', 'authenticated', 'service_role'] LOOP
                            -- PostgreSQL thường (CI, máy dev) không có các role API của Supabase.
                            IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = api_role) THEN
                                EXECUTE format('REVOKE ALL PRIVILEGES ON TABLE public.%I FROM %I', '{table}', api_role);
                            END IF;
                        END LOOP;
                    END;
                    $rebuild_security$;
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Cố ý giữ RLS và quyền đã thu hồi: rollback không được mở lại dữ liệu cá nhân và tiền cọc.
        }
    }
}
