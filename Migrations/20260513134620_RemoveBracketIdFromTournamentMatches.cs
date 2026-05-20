using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RankingDigi.Migrations
{
    /// <inheritdoc />
    public partial class RemoveBracketIdFromTournamentMatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Brackets_Tournaments_TournamentId",
                table: "Brackets");

            migrationBuilder.DropForeignKey(
                name: "FK_TournamentMatches_Brackets_BracketId",
                table: "TournamentMatches");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Brackets",
                table: "Brackets");

            migrationBuilder.RenameTable(
                name: "Brackets",
                newName: "Bracket");

            migrationBuilder.RenameIndex(
                name: "IX_Brackets_TournamentId",
                table: "Bracket",
                newName: "IX_Bracket_TournamentId");

            migrationBuilder.AlterColumn<int>(
                name: "BracketId",
                table: "TournamentMatches",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Bracket",
                table: "Bracket",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Bracket_Tournaments_TournamentId",
                table: "Bracket",
                column: "TournamentId",
                principalTable: "Tournaments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TournamentMatches_Bracket_BracketId",
                table: "TournamentMatches",
                column: "BracketId",
                principalTable: "Bracket",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bracket_Tournaments_TournamentId",
                table: "Bracket");

            migrationBuilder.DropForeignKey(
                name: "FK_TournamentMatches_Bracket_BracketId",
                table: "TournamentMatches");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Bracket",
                table: "Bracket");

            migrationBuilder.RenameTable(
                name: "Bracket",
                newName: "Brackets");

            migrationBuilder.RenameIndex(
                name: "IX_Bracket_TournamentId",
                table: "Brackets",
                newName: "IX_Brackets_TournamentId");

            migrationBuilder.AlterColumn<int>(
                name: "BracketId",
                table: "TournamentMatches",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Brackets",
                table: "Brackets",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Brackets_Tournaments_TournamentId",
                table: "Brackets",
                column: "TournamentId",
                principalTable: "Tournaments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TournamentMatches_Brackets_BracketId",
                table: "TournamentMatches",
                column: "BracketId",
                principalTable: "Brackets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
