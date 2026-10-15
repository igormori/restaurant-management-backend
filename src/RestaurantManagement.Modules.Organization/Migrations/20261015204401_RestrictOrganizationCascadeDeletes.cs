using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantManagement.Modules.Organization.Migrations
{
    /// <inheritdoc />
    public partial class RestrictOrganizationCascadeDeletes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_locations_organizations_organization_id",
                table: "locations");

            migrationBuilder.DropForeignKey(
                name: "fk_organization_settings_organizations_organization_id",
                table: "organization_settings");

            migrationBuilder.AddForeignKey(
                name: "fk_locations_organizations_organization_id",
                table: "locations",
                column: "organization_id",
                principalTable: "organizations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_organization_settings_organizations_organization_id",
                table: "organization_settings",
                column: "organization_id",
                principalTable: "organizations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_locations_organizations_organization_id",
                table: "locations");

            migrationBuilder.DropForeignKey(
                name: "fk_organization_settings_organizations_organization_id",
                table: "organization_settings");

            migrationBuilder.AddForeignKey(
                name: "fk_locations_organizations_organization_id",
                table: "locations",
                column: "organization_id",
                principalTable: "organizations",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_organization_settings_organizations_organization_id",
                table: "organization_settings",
                column: "organization_id",
                principalTable: "organizations",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
