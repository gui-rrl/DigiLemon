using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RankingDigi.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentRegistrationDeadline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RegistrationDeadline",
                table: "Tournaments",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RegistrationDeadline",
                table: "Tournaments");
        }
    }
}
