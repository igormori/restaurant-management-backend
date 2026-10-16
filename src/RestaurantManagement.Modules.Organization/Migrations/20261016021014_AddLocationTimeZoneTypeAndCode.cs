using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantManagement.Modules.Organization.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationTimeZoneTypeAndCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_locations_organization_id",
                table: "locations");

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "locations",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "time_zone",
                table: "locations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "locations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Store");

            migrationBuilder.CreateIndex(
                name: "ix_locations_organization_id_code",
                table: "locations",
                columns: new[] { "organization_id", "code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_locations_organization_id_code",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "code",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "time_zone",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "type",
                table: "locations");

            migrationBuilder.CreateIndex(
                name: "ix_locations_organization_id",
                table: "locations",
                column: "organization_id");
        }
    }
}
