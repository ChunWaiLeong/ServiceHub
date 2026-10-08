using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProtectConfirmedBookingIntervals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Shared extension lives outside isolated test schemas. UUID equality needs its GiST operator class.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist WITH SCHEMA public;");
            migrationBuilder.Sql("""
                ALTER TABLE "Bookings" ADD CONSTRAINT "EX_Bookings_ConfirmedBusinessOverlap"
                EXCLUDE USING gist (
                    "BusinessId" public.gist_uuid_ops WITH =,
                    tstzrange("StartUtc", "EndUtc", '[)') WITH &&
                ) WHERE ("Status" = 'Confirmed');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"Bookings\" DROP CONSTRAINT \"EX_Bookings_ConfirmedBusinessOverlap\";");
            // Keep the shared extension: other schemas/applications can depend on it.
        }
    }
}
