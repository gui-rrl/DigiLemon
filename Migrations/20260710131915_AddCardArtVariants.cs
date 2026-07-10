using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RankingDigi.Migrations
{
    /// <inheritdoc />
    public partial class AddCardArtVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TcgplayerId",
                table: "DeckCards",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CardArts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CardNumber = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TcgplayerId = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardArts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardArts_Cards_CardNumber",
                        column: x => x.CardNumber,
                        principalTable: "Cards",
                        principalColumn: "CardNumber",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CardArts_CardNumber_TcgplayerId",
                table: "CardArts",
                columns: new[] { "CardNumber", "TcgplayerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CardArts");

            migrationBuilder.DropColumn(
                name: "TcgplayerId",
                table: "DeckCards");
        }
    }
}
