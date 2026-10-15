using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantManagement.Modules.Identity.Migrations
{
    /// <inheritdoc />
    public partial class RetireUnhashedVerificationCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rows from before release hold readable codes in code_hash. Retire them so no
            // readable code stays in the database; created_at is kept for the resend cooldown.
            migrationBuilder.Sql("UPDATE user_verification_codes SET is_used = TRUE, code_hash = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The readable codes cannot be restored, so there is no data change to undo.
        }
    }
}
