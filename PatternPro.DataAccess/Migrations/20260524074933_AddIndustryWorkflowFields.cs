using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatternPro.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddIndustryWorkflowFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MeasurementMethod",
                schema: "patternpro",
                table: "size_chart_rows",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ToleranceCm",
                schema: "patternpro",
                table: "size_chart_rows",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Material",
                schema: "patternpro",
                table: "pieces",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "OnFold",
                schema: "patternpro",
                table: "pieces",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PieceNumber",
                schema: "patternpro",
                table: "pieces",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "FabricStretchPercent",
                schema: "patternpro",
                table: "patterns",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Revision",
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
                name: "MeasurementMethod",
                schema: "patternpro",
                table: "size_chart_rows");

            migrationBuilder.DropColumn(
                name: "ToleranceCm",
                schema: "patternpro",
                table: "size_chart_rows");

            migrationBuilder.DropColumn(
                name: "Material",
                schema: "patternpro",
                table: "pieces");

            migrationBuilder.DropColumn(
                name: "OnFold",
                schema: "patternpro",
                table: "pieces");

            migrationBuilder.DropColumn(
                name: "PieceNumber",
                schema: "patternpro",
                table: "pieces");

            migrationBuilder.DropColumn(
                name: "FabricStretchPercent",
                schema: "patternpro",
                table: "patterns");

            migrationBuilder.DropColumn(
                name: "Revision",
                schema: "patternpro",
                table: "patterns");
        }
    }
}
