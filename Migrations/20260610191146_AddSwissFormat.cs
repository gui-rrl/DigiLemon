using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RankingDigi.Migrations
{
    /// <inheritdoc />
    public partial class AddSwissFormat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentSwissRound",
                table: "Tournaments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Format",
                table: "Tournaments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SwissRounds",
                table: "Tournaments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TopCutSize",
                table: "Tournaments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SwissDraws",
                table: "TournamentPlayers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SwissLosses",
                table: "TournamentPlayers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SwissPoints",
                table: "TournamentPlayers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SwissWins",
                table: "TournamentPlayers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsBye",
                table: "TournamentMatches",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentSwissRound",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "Format",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "SwissRounds",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "TopCutSize",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "SwissDraws",
                table: "TournamentPlayers");

            migrationBuilder.DropColumn(
                name: "SwissLosses",
                table: "TournamentPlayers");

            migrationBuilder.DropColumn(
                name: "SwissPoints",
                table: "TournamentPlayers");

            migrationBuilder.DropColumn(
                name: "SwissWins",
                table: "TournamentPlayers");

            migrationBuilder.DropColumn(
                name: "IsBye",
                table: "TournamentMatches");
        }
    }
}
