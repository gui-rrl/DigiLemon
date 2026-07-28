using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RankingDigi.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchGameScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Player1GameWins",
                table: "TournamentMatches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Player2GameWins",
                table: "TournamentMatches",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Player1GameWins",
                table: "TournamentMatches");

            migrationBuilder.DropColumn(
                name: "Player2GameWins",
                table: "TournamentMatches");
        }
    }
}
