using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatternPro.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionCertificationColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                schema: "patternpro",
                table: "patterns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedBy",
                schema: "patternpro",
                table: "patterns",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "ApprovedForCutting",
                schema: "patternpro",
                table: "patterns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CloReviewCompleted",
                schema: "patternpro",
                table: "patterns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CloReviewNotes",
                schema: "patternpro",
                table: "patterns",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CutterTestNotes",
                schema: "patternpro",
                table: "patterns",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "CutterTestPassed",
                schema: "patternpro",
                table: "patterns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CutterTestedAt",
                schema: "patternpro",
                table: "patterns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CutterTestedBy",
                schema: "patternpro",
                table: "patterns",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ShrinkagePercent",
                schema: "patternpro",
                table: "patterns",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                schema: "patternpro",
                table: "patterns");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                schema: "patternpro",
                table: "patterns");

            migrationBuilder.DropColumn(
                name: "ApprovedForCutting",
                schema: "patternpro",
                table: "patterns");

            migrationBuilder.DropColumn(
                name: "CloReviewCompleted",
                schema: "patternpro",
                table: "patterns");

            migrationBuilder.DropColumn(
                name: "CloReviewNotes",
                schema: "patternpro",
                table: "patterns");

            migrationBuilder.DropColumn(
                name: "CutterTestNotes",
                schema: "patternpro",
                table: "patterns");

            migrationBuilder.DropColumn(
                name: "CutterTestPassed",
                schema: "patternpro",
                table: "patterns");

            migrationBuilder.DropColumn(
                name: "CutterTestedAt",
                schema: "patternpro",
                table: "patterns");

            migrationBuilder.DropColumn(
                name: "CutterTestedBy",
                schema: "patternpro",
                table: "patterns");

            migrationBuilder.DropColumn(
                name: "ShrinkagePercent",
                schema: "patternpro",
                table: "patterns");
        }
    }
}
