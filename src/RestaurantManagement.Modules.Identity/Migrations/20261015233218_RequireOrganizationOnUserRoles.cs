using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantManagement.Modules.Identity.Migrations
{
    /// <inheritdoc />
    public partial class RequireOrganizationOnUserRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_user_roles_user_id",
                table: "user_roles");

            migrationBuilder.AlterColumn<Guid>(
                name: "organization_id",
                table: "user_roles",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_user_id_organization_id_location_id_role",
                table: "user_roles",
                columns: new[] { "user_id", "organization_id", "location_id", "role" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_roles_organization_id_not_empty",
                table: "user_roles",
                sql: "organization_id <> '00000000-0000-0000-0000-000000000000'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_user_roles_user_id_organization_id_location_id_role",
                table: "user_roles");

            migrationBuilder.DropCheckConstraint(
                name: "ck_user_roles_organization_id_not_empty",
                table: "user_roles");

            migrationBuilder.AlterColumn<Guid>(
                name: "organization_id",
                table: "user_roles",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_user_id",
                table: "user_roles",
                column: "user_id");
        }
    }
}
