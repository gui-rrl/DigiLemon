using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RankingDigi.Migrations
{
    /// <inheritdoc />
    public partial class AddDcgoMatchReporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Player1ReportCode",
                table: "TournamentMatches",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Player2ReportCode",
                table: "TournamentMatches",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MatchReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TournamentMatchId = table.Column<int>(type: "int", nullable: false),
                    PlayerSlot = table.Column<int>(type: "int", nullable: false),
                    ReporterTournamentPlayerId = table.Column<int>(type: "int", nullable: false),
                    ClaimedWinnerTpId = table.Column<int>(type: "int", nullable: true),
                    ClaimedPlayer1GameWins = table.Column<int>(type: "int", nullable: false),
                    ClaimedPlayer2GameWins = table.Column<int>(type: "int", nullable: false),
                    ReporterNickname = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ClientVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SourceIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RevisionCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchReports_TournamentMatches_TournamentMatchId",
                        column: x => x.TournamentMatchId,
                        principalTable: "TournamentMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TournamentMatches_Player1ReportCode",
                table: "TournamentMatches",
                column: "Player1ReportCode",
                unique: true,
                filter: "[Player1ReportCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentMatches_Player2ReportCode",
                table: "TournamentMatches",
                column: "Player2ReportCode",
                unique: true,
                filter: "[Player2ReportCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MatchReports_MatchId_Slot",
                table: "MatchReports",
                columns: new[] { "TournamentMatchId", "PlayerSlot" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchReports");

            migrationBuilder.DropIndex(
                name: "IX_TournamentMatches_Player1ReportCode",
                table: "TournamentMatches");

            migrationBuilder.DropIndex(
                name: "IX_TournamentMatches_Player2ReportCode",
                table: "TournamentMatches");

            migrationBuilder.DropColumn(
                name: "Player1ReportCode",
                table: "TournamentMatches");

            migrationBuilder.DropColumn(
                name: "Player2ReportCode",
                table: "TournamentMatches");
        }
    }
}
