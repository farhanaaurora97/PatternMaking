using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatternPro.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeIdToAppUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmployeeId",
                schema: "patternpro",
                table: "app_users",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE patternpro.app_users
                SET "EmployeeId" = 'ADMIN'
                WHERE LOWER("UserName") = 'admin' AND "EmployeeId" = '';

                UPDATE patternpro.app_users
                SET "EmployeeId" = 'EMP-' || "Id"::text
                WHERE "EmployeeId" = '';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_app_users_EmployeeId",
                schema: "patternpro",
                table: "app_users",
                column: "EmployeeId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_app_users_EmployeeId",
                schema: "patternpro",
                table: "app_users");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                schema: "patternpro",
                table: "app_users");
        }
    }
}
