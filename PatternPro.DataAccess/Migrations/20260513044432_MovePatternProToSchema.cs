using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatternPro.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class MovePatternProToSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "patternpro");

            migrationBuilder.RenameTable(
                name: "patterns",
                newName: "patterns",
                newSchema: "patternpro");

            migrationBuilder.RenameTable(
                name: "app_kv",
                newName: "app_kv",
                newSchema: "patternpro");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "patterns",
                schema: "patternpro",
                newName: "patterns");

            migrationBuilder.RenameTable(
                name: "app_kv",
                schema: "patternpro",
                newName: "app_kv");
        }
    }
}
