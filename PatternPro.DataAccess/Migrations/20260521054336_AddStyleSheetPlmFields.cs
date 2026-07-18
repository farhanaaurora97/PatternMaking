using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatternPro.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddStyleSheetPlmFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LifecycleStatus",
                schema: "patternpro",
                table: "patterns",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Owner",
                schema: "patternpro",
                table: "patterns",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Season",
                schema: "patternpro",
                table: "patterns",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LifecycleStatus",
                schema: "patternpro",
                table: "patterns");

            migrationBuilder.DropColumn(
                name: "Owner",
                schema: "patternpro",
                table: "patterns");

            migrationBuilder.DropColumn(
                name: "Season",
                schema: "patternpro",
                table: "patterns");
        }
    }
}
