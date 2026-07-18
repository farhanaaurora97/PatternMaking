using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatternPro.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddPatternChartSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChartMode",
                schema: "patternpro",
                table: "patterns",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "UseCustomSizeChart",
                schema: "patternpro",
                table: "patterns",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChartMode",
                schema: "patternpro",
                table: "patterns");

            migrationBuilder.DropColumn(
                name: "UseCustomSizeChart",
                schema: "patternpro",
                table: "patterns");
        }
    }
}
