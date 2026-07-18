using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PatternPro.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class NormalizePiecesStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pieces",
                schema: "patternpro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatternId = table.Column<int>(type: "integer", nullable: true),
                    StyleKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PieceOrder = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Cut = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Color = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Category = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    GrainLine = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    OffsetX = table.Column<int>(type: "integer", nullable: false),
                    OffsetY = table.Column<int>(type: "integer", nullable: false),
                    SeamAllowance = table.Column<double>(type: "double precision", nullable: false),
                    SeamAllowanceJoin = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pieces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "piece_vertices",
                schema: "patternpro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PieceId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PointOrder = table.Column<int>(type: "integer", nullable: false),
                    X = table.Column<int>(type: "integer", nullable: false),
                    Y = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_piece_vertices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_piece_vertices_pieces_PieceId",
                        column: x => x.PieceId,
                        principalSchema: "patternpro",
                        principalTable: "pieces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_piece_vertices_PieceId_Kind_PointOrder",
                schema: "patternpro",
                table: "piece_vertices",
                columns: new[] { "PieceId", "Kind", "PointOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_pieces_PatternId",
                schema: "patternpro",
                table: "pieces",
                column: "PatternId");

            migrationBuilder.CreateIndex(
                name: "IX_pieces_StyleKey",
                schema: "patternpro",
                table: "pieces",
                column: "StyleKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "piece_vertices",
                schema: "patternpro");

            migrationBuilder.DropTable(
                name: "pieces",
                schema: "patternpro");
        }
    }
}
