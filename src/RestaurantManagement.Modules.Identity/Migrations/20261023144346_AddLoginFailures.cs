using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantManagement.Modules.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddLoginFailures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "failed_attempts",
                table: "users");

            migrationBuilder.CreateTable(
                name: "login_failures",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    failed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    counts_for_address = table.Column<bool>(type: "boolean", nullable: false),
                    counts_for_account = table.Column<bool>(type: "boolean", nullable: false),
                    address_locked_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_login_failures", x => x.id);
                    table.ForeignKey(
                        name: "fk_login_failures_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_login_failures_user_id_failed_at",
                table: "login_failures",
                columns: new[] { "user_id", "failed_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "login_failures");

            migrationBuilder.AddColumn<int>(
                name: "failed_attempts",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
