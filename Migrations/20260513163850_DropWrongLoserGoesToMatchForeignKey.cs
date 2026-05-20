using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RankingDigi.Migrations
{
    /// <inheritdoc />
    public partial class DropWrongLoserGoesToMatchForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove a FK que referencia a tabela Tournaments (incorreta)
            migrationBuilder.DropForeignKey(
                name: "FK_TournamentMatches_Tournaments_LoserGoesToMatchId",
                table: "TournamentMatches");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
