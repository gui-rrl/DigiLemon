using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RankingDigi.Migrations
{
    /// <inheritdoc />
    public partial class AddDoubleEliminationColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LoserGoesToMatchId",
                table: "TournamentMatches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MatchType",
                table: "TournamentMatches",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Round",
                table: "TournamentMatches",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TournamentId",
                table: "TournamentMatches",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_TournamentMatches_LoserGoesToMatchId",
                table: "TournamentMatches",
                column: "LoserGoesToMatchId");

            migrationBuilder.AddForeignKey(
                name: "FK_TournamentMatches_Tournaments_LoserGoesToMatchId",
                table: "TournamentMatches",
                column: "LoserGoesToMatchId",
                principalTable: "Tournaments",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TournamentMatches_Tournaments_LoserGoesToMatchId",
                table: "TournamentMatches");

            migrationBuilder.DropIndex(
                name: "IX_TournamentMatches_LoserGoesToMatchId",
                table: "TournamentMatches");

            migrationBuilder.DropColumn(
                name: "LoserGoesToMatchId",
                table: "TournamentMatches");

            migrationBuilder.DropColumn(
                name: "MatchType",
                table: "TournamentMatches");

            migrationBuilder.DropColumn(
                name: "Round",
                table: "TournamentMatches");

            migrationBuilder.DropColumn(
                name: "TournamentId",
                table: "TournamentMatches");
        }
    }
}
