using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RankingDigi.Migrations
{
    /// <inheritdoc />
    public partial class LinkDeckToMatchesAndTournaments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DeckId",
                table: "TournamentPlayers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Deck1Id",
                table: "Matches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Deck2Id",
                table: "Matches",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TournamentPlayers_DeckId",
                table: "TournamentPlayers",
                column: "DeckId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_Deck1Id",
                table: "Matches",
                column: "Deck1Id");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_Deck2Id",
                table: "Matches",
                column: "Deck2Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_Decks_Deck1Id",
                table: "Matches",
                column: "Deck1Id",
                principalTable: "Decks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_Decks_Deck2Id",
                table: "Matches",
                column: "Deck2Id",
                principalTable: "Decks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TournamentPlayers_Decks_DeckId",
                table: "TournamentPlayers",
                column: "DeckId",
                principalTable: "Decks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Matches_Decks_Deck1Id",
                table: "Matches");

            migrationBuilder.DropForeignKey(
                name: "FK_Matches_Decks_Deck2Id",
                table: "Matches");

            migrationBuilder.DropForeignKey(
                name: "FK_TournamentPlayers_Decks_DeckId",
                table: "TournamentPlayers");

            migrationBuilder.DropIndex(
                name: "IX_TournamentPlayers_DeckId",
                table: "TournamentPlayers");

            migrationBuilder.DropIndex(
                name: "IX_Matches_Deck1Id",
                table: "Matches");

            migrationBuilder.DropIndex(
                name: "IX_Matches_Deck2Id",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "DeckId",
                table: "TournamentPlayers");

            migrationBuilder.DropColumn(
                name: "Deck1Id",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "Deck2Id",
                table: "Matches");
        }
    }
}
