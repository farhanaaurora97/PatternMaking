using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PatternPro.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddRelationalAppDataTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ease_overrides",
                schema: "patternpro",
                columns: table => new
                {
                    StyleKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MeasurementPoint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Value = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ease_overrides", x => new { x.StyleKey, x.MeasurementPoint });
                });

            migrationBuilder.CreateTable(
                name: "grading_columns",
                schema: "patternpro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grading_columns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "grading_meta",
                schema: "patternpro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BaseIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grading_meta", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "grading_styles",
                schema: "patternpro",
                columns: table => new
                {
                    StyleKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Label = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grading_styles", x => x.StyleKey);
                });

            migrationBuilder.CreateTable(
                name: "measurement_profiles",
                schema: "patternpro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_measurement_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "size_chart_columns",
                schema: "patternpro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_size_chart_columns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "size_chart_rows",
                schema: "patternpro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    MeasurementPoint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_size_chart_rows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "grading_rows",
                schema: "patternpro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StyleKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    MeasurementPoint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    BaseIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grading_rows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_grading_rows_grading_styles_StyleKey",
                        column: x => x.StyleKey,
                        principalSchema: "patternpro",
                        principalTable: "grading_styles",
                        principalColumn: "StyleKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "measurement_profile_values",
                schema: "patternpro",
                columns: table => new
                {
                    ProfileId = table.Column<int>(type: "integer", nullable: false),
                    MeasurementPoint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Value = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_measurement_profile_values", x => new { x.ProfileId, x.MeasurementPoint });
                    table.ForeignKey(
                        name: "FK_measurement_profile_values_measurement_profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalSchema: "patternpro",
                        principalTable: "measurement_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "size_chart_values",
                schema: "patternpro",
                columns: table => new
                {
                    RowId = table.Column<int>(type: "integer", nullable: false),
                    ColumnIndex = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<decimal>(type: "numeric", nullable: false),
                    SizeChartColumnEntityId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_size_chart_values", x => new { x.RowId, x.ColumnIndex });
                    table.ForeignKey(
                        name: "FK_size_chart_values_size_chart_columns_SizeChartColumnEntityId",
                        column: x => x.SizeChartColumnEntityId,
                        principalSchema: "patternpro",
                        principalTable: "size_chart_columns",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_size_chart_values_size_chart_rows_RowId",
                        column: x => x.RowId,
                        principalSchema: "patternpro",
                        principalTable: "size_chart_rows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "grading_deltas",
                schema: "patternpro",
                columns: table => new
                {
                    RowId = table.Column<int>(type: "integer", nullable: false),
                    ColumnIndex = table.Column<int>(type: "integer", nullable: false),
                    Delta = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grading_deltas", x => new { x.RowId, x.ColumnIndex });
                    table.ForeignKey(
                        name: "FK_grading_deltas_grading_rows_RowId",
                        column: x => x.RowId,
                        principalSchema: "patternpro",
                        principalTable: "grading_rows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_grading_columns_SortOrder",
                schema: "patternpro",
                table: "grading_columns",
                column: "SortOrder",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_grading_rows_StyleKey_MeasurementPoint",
                schema: "patternpro",
                table: "grading_rows",
                columns: new[] { "StyleKey", "MeasurementPoint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_measurement_profiles_Name",
                schema: "patternpro",
                table: "measurement_profiles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_size_chart_columns_SortOrder",
                schema: "patternpro",
                table: "size_chart_columns",
                column: "SortOrder",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_size_chart_rows_SortOrder",
                schema: "patternpro",
                table: "size_chart_rows",
                column: "SortOrder",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_size_chart_values_SizeChartColumnEntityId",
                schema: "patternpro",
                table: "size_chart_values",
                column: "SizeChartColumnEntityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ease_overrides",
                schema: "patternpro");

            migrationBuilder.DropTable(
                name: "grading_columns",
                schema: "patternpro");

            migrationBuilder.DropTable(
                name: "grading_deltas",
                schema: "patternpro");

            migrationBuilder.DropTable(
                name: "grading_meta",
                schema: "patternpro");

            migrationBuilder.DropTable(
                name: "measurement_profile_values",
                schema: "patternpro");

            migrationBuilder.DropTable(
                name: "size_chart_values",
                schema: "patternpro");

            migrationBuilder.DropTable(
                name: "grading_rows",
                schema: "patternpro");

            migrationBuilder.DropTable(
                name: "measurement_profiles",
                schema: "patternpro");

            migrationBuilder.DropTable(
                name: "size_chart_columns",
                schema: "patternpro");

            migrationBuilder.DropTable(
                name: "size_chart_rows",
                schema: "patternpro");

            migrationBuilder.DropTable(
                name: "grading_styles",
                schema: "patternpro");
        }
    }
}
