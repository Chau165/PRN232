using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN232.Infrastructure.Migrations;

public partial class SecurePaymentTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // These tables are internal to the .NET API. No browser/Data API policy is created.
        // Explicit public schema and quoted identifiers match the application's existing migrations.
        foreach (var table in new[] { "PaymentRequests", "PaymentReviewCases", "PaymentWebhookReceipts",
                     "BookingRefundRequests", "PayoutAttempts", "BookingRefunds" })
        {
            migrationBuilder.Sql($"""
                ALTER TABLE public."{table}" ENABLE ROW LEVEL SECURITY;
                REVOKE ALL PRIVILEGES ON TABLE public."{table}" FROM PUBLIC;
                DO $payment_security$
                DECLARE
                    api_role text;
                    identity_sequence text;
                BEGIN
                    identity_sequence := pg_get_serial_sequence('public."{table}"',
                        CASE '{table}'
                            WHEN 'PaymentRequests' THEN 'PaymentRequestId'
                            WHEN 'PaymentReviewCases' THEN 'PaymentReviewCaseId'
                            WHEN 'PaymentWebhookReceipts' THEN 'PaymentWebhookReceiptId'
                            WHEN 'BookingRefundRequests' THEN 'BookingRefundRequestId'
                            WHEN 'PayoutAttempts' THEN 'PayoutAttemptId'
                            WHEN 'BookingRefunds' THEN 'BookingRefundId'
                        END);
                    IF identity_sequence IS NOT NULL THEN
                        EXECUTE format('REVOKE ALL PRIVILEGES ON SEQUENCE %s FROM PUBLIC', identity_sequence::regclass);
                    END IF;
                    FOREACH api_role IN ARRAY ARRAY['anon', 'authenticated', 'service_role'] LOOP
                        -- Plain PostgreSQL installations do not have Supabase API roles.
                        IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = api_role) THEN
                            EXECUTE format('REVOKE ALL PRIVILEGES ON TABLE public.%I FROM %I', '{table}', api_role);
                            IF identity_sequence IS NOT NULL THEN
                                EXECUTE format('REVOKE ALL PRIVILEGES ON SEQUENCE %s FROM %I', identity_sequence::regclass, api_role);
                            END IF;
                        END IF;
                    END LOOP;
                    -- Do not silently claim success if custom role inheritance or column grants
                    -- still expose the table. The migration transaction rolls back on this error.
                    FOREACH api_role IN ARRAY ARRAY['anon', 'authenticated', 'service_role'] LOOP
                        IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = api_role) THEN
                            IF identity_sequence IS NOT NULL AND has_sequence_privilege(api_role, identity_sequence, 'USAGE,SELECT,UPDATE') THEN
                                RAISE EXCEPTION 'Unexpected inherited API sequence access for %. Review grants before applying payment security.', '{table}';
                            END IF;
                            IF has_table_privilege(api_role, 'public."{table}"', 'SELECT,INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER')
                               OR has_any_column_privilege(api_role, 'public."{table}"', 'SELECT,INSERT,UPDATE,REFERENCES') THEN
                                RAISE EXCEPTION 'Unexpected inherited or column-level API access to %. Review grants before applying payment security.', '{table}';
                            END IF;
                        END IF;
                    END LOOP;
                END;
                $payment_security$;
                """);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Deliberately retain RLS and revoked grants. A rollback must not reopen financial data.
        // Any restoration of access requires a separately reviewed, explicit permission change.
    }
}
