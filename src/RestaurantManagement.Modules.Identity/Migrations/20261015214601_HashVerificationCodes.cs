using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantManagement.Modules.Identity.Migrations
{
    /// <inheritdoc />
    public partial class HashVerificationCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "code",
                table: "user_verification_codes",
                newName: "code_hash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "code_hash",
                table: "user_verification_codes",
                newName: "code");
        }
    }
}
