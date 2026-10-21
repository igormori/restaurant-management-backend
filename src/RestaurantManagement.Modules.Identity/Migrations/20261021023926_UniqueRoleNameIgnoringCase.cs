using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantManagement.Modules.Identity.Migrations
{
    /// <inheritdoc />
    public partial class UniqueRoleNameIgnoringCase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_organization_roles_organization_id_name",
                table: "organization_roles");

            migrationBuilder.AddColumn<string>(
                name: "name_normalized",
                table: "organization_roles",
                type: "text",
                nullable: true,
                computedColumnSql: "lower(name)",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "ix_organization_roles_organization_id_name",
                table: "organization_roles",
                columns: new[] { "organization_id", "name_normalized" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_organization_roles_organization_id_name",
                table: "organization_roles");

            migrationBuilder.DropColumn(
                name: "name_normalized",
                table: "organization_roles");

            migrationBuilder.CreateIndex(
                name: "ix_organization_roles_organization_id_name",
                table: "organization_roles",
                columns: new[] { "organization_id", "name" },
                unique: true);
        }
    }
}
