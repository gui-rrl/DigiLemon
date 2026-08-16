using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RankingDigi.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentDeckModes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DeckDrawCompleted",
                table: "Tournaments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "DeckMode",
                table: "Tournaments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DeckPoolSize",
                table: "Tournaments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SourceDeckId",
                table: "Decks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceTournamentId",
                table: "Decks",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TournamentPlayerDeckOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TournamentPlayerId = table.Column<int>(type: "int", nullable: false),
                    DeckId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentPlayerDeckOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentPlayerDeckOptions_Decks_DeckId",
                        column: x => x.DeckId,
                        principalTable: "Decks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TournamentPlayerDeckOptions_TournamentPlayers_TournamentPlayerId",
                        column: x => x.TournamentPlayerId,
                        principalTable: "TournamentPlayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Decks_SourceDeckId",
                table: "Decks",
                column: "SourceDeckId");

            migrationBuilder.CreateIndex(
                name: "IX_Decks_SourceTournamentId",
                table: "Decks",
                column: "SourceTournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentPlayerDeckOptions_DeckId",
                table: "TournamentPlayerDeckOptions",
                column: "DeckId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentPlayerDeckOptions_TournamentPlayerId",
                table: "TournamentPlayerDeckOptions",
                column: "TournamentPlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Decks_Decks_SourceDeckId",
                table: "Decks",
                column: "SourceDeckId",
                principalTable: "Decks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Decks_Tournaments_SourceTournamentId",
                table: "Decks",
                column: "SourceTournamentId",
                principalTable: "Tournaments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Decks_Decks_SourceDeckId",
                table: "Decks");

            migrationBuilder.DropForeignKey(
                name: "FK_Decks_Tournaments_SourceTournamentId",
                table: "Decks");

            migrationBuilder.DropTable(
                name: "TournamentPlayerDeckOptions");

            migrationBuilder.DropIndex(
                name: "IX_Decks_SourceDeckId",
                table: "Decks");

            migrationBuilder.DropIndex(
                name: "IX_Decks_SourceTournamentId",
                table: "Decks");

            migrationBuilder.DropColumn(
                name: "DeckDrawCompleted",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "DeckMode",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "DeckPoolSize",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "SourceDeckId",
                table: "Decks");

            migrationBuilder.DropColumn(
                name: "SourceTournamentId",
                table: "Decks");
        }
    }
}
