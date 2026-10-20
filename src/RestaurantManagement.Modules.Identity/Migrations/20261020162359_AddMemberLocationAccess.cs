using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantManagement.Modules.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberLocationAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "member_location_levels",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    module = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_member_location_levels", x => x.id);
                    table.ForeignKey(
                        name: "fk_member_location_levels_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "member_location_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_member_location_roles", x => x.id);
                    table.ForeignKey(
                        name: "fk_member_location_roles_organization_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "organization_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_member_location_roles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_member_location_levels_cell",
                table: "member_location_levels",
                columns: new[] { "organization_id", "user_id", "location_id", "module" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_member_location_levels_user_id",
                table: "member_location_levels",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_member_location_roles_label",
                table: "member_location_roles",
                columns: new[] { "organization_id", "user_id", "location_id", "role_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_member_location_roles_role_id",
                table: "member_location_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_member_location_roles_user_id",
                table: "member_location_roles",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "member_location_levels");

            migrationBuilder.DropTable(
                name: "member_location_roles");
        }
    }
}
