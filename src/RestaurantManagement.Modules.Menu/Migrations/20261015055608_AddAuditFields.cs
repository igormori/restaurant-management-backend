using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantManagement.Modules.Menu.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                table: "menus",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_user_id",
                table: "menus",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                table: "menu_categories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "menu_categories",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_user_id",
                table: "menu_categories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                table: "location_menus",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "location_menus",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "location_menus",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_user_id",
                table: "location_menus",
                type: "uuid",
                nullable: true);

            // Backfill (flagged in the plan): existing rows get their organization from the parent menu
            // and their update time from the creation time, so the columns can become NOT NULL.
            migrationBuilder.Sql(
                """
                UPDATE location_menus
                SET organization_id = menus.organization_id
                FROM menus
                WHERE location_menus.menu_id = menus.id;
                """);

            migrationBuilder.Sql("UPDATE location_menus SET updated_at = created_at;");
            migrationBuilder.Sql("UPDATE menu_categories SET updated_at = created_at;");

            migrationBuilder.AlterColumn<Guid>(
                name: "organization_id",
                table: "location_menus",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                table: "location_menus",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                table: "menu_categories",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                table: "menus");

            migrationBuilder.DropColumn(
                name: "updated_by_user_id",
                table: "menus");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                table: "menu_categories");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "menu_categories");

            migrationBuilder.DropColumn(
                name: "updated_by_user_id",
                table: "menu_categories");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                table: "location_menus");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "location_menus");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "location_menus");

            migrationBuilder.DropColumn(
                name: "updated_by_user_id",
                table: "location_menus");
        }
    }
}
