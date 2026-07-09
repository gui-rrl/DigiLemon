using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RankingDigi.Migrations
{
    /// <inheritdoc />
    public partial class AddDeckBuilder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BannedPairs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CardNumberA = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CardNumberB = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BannedPairs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CardRestrictions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CardNumber = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MaxCopies = table.Column<int>(type: "int", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardRestrictions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CardNumber = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: true),
                    PlayCost = table.Column<int>(type: "int", nullable: true),
                    EvolutionCost = table.Column<int>(type: "int", nullable: true),
                    EvolutionColor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EvolutionLevel = table.Column<int>(type: "int", nullable: true),
                    Color = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Color2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DigiType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Dp = table.Column<int>(type: "int", nullable: true),
                    Attribute = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Rarity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Stage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MainEffect = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceEffect = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SetName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cards", x => x.Id);
                    table.UniqueConstraint("AK_Cards_CardNumber", x => x.CardNumber);
                });

            migrationBuilder.CreateTable(
                name: "Decks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Decks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Decks_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeckCards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeckId = table.Column<int>(type: "int", nullable: false),
                    CardNumber = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    IsDigiEgg = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeckCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeckCards_Cards_CardNumber",
                        column: x => x.CardNumber,
                        principalTable: "Cards",
                        principalColumn: "CardNumber",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeckCards_Decks_DeckId",
                        column: x => x.DeckId,
                        principalTable: "Decks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CardRestrictions_CardNumber",
                table: "CardRestrictions",
                column: "CardNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cards_CardNumber",
                table: "Cards",
                column: "CardNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeckCards_CardNumber",
                table: "DeckCards",
                column: "CardNumber");

            migrationBuilder.CreateIndex(
                name: "IX_DeckCards_DeckId_CardNumber_IsDigiEgg",
                table: "DeckCards",
                columns: new[] { "DeckId", "CardNumber", "IsDigiEgg" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Decks_PlayerId",
                table: "Decks",
                column: "PlayerId");

            // Lista oficial de banidos/restritos do Digimon Card Game, efetiva em 04/04/2026
            migrationBuilder.Sql(@"
                INSERT INTO CardRestrictions (CardNumber, MaxCopies, EffectiveDate) VALUES
                ('BT5-109', 0, '2026-04-04'),
                ('BT2-090', 0, '2026-04-04'),
                ('EX5-065', 0, '2026-04-04'),
                ('BT23-032', 1, '2026-04-04'),
                ('BT3-092', 1, '2026-04-04'),
                ('BT10-080', 1, '2026-04-04'),
                ('EX5-059', 1, '2026-04-04'),
                ('EX5-061', 1, '2026-04-04'),
                ('BT1-090', 1, '2026-04-04'),
                ('BT6-104', 1, '2026-04-04'),
                ('BT13-110', 1, '2026-04-04'),
                ('BT16-011', 1, '2026-04-04'),
                ('EX3-057', 1, '2026-04-04'),
                ('EX4-006', 1, '2026-04-04'),
                ('EX1-021', 1, '2026-04-04'),
                ('BT19-040', 1, '2026-04-04'),
                ('EX2-070', 1, '2026-04-04'),
                ('BT4-111', 1, '2026-04-04'),
                ('BT17-069', 1, '2026-04-04'),
                ('BT4-104', 1, '2026-04-04'),
                ('P-029', 1, '2026-04-04'),
                ('P-030', 1, '2026-04-04'),
                ('BT11-033', 1, '2026-04-04'),
                ('ST9-09', 1, '2026-04-04'),
                ('EX4-030', 1, '2026-04-04'),
                ('P-123', 1, '2026-04-04'),
                ('P-130', 1, '2026-04-04'),
                ('BT15-057', 1, '2026-04-04'),
                ('BT9-098', 1, '2026-04-04'),
                ('ST2-13', 1, '2026-04-04'),
                ('BT14-084', 1, '2026-04-04'),
                ('BT14-002', 1, '2026-04-04'),
                ('BT15-102', 1, '2026-04-04'),
                ('EX5-015', 1, '2026-04-04'),
                ('EX5-018', 1, '2026-04-04'),
                ('EX5-062', 1, '2026-04-04'),
                ('BT13-012', 1, '2026-04-04'),
                ('BT2-069', 1, '2026-04-04'),
                ('BT7-069', 1, '2026-04-04'),
                ('BT3-054', 1, '2026-04-04'),
                ('EX2-039', 1, '2026-04-04'),
                ('P-008', 1, '2026-04-04'),
                ('P-025', 1, '2026-04-04'),
                ('BT11-064', 1, '2026-04-04'),
                ('BT7-107', 1, '2026-04-04'),
                ('BT10-009', 1, '2026-04-04'),
                ('BT7-038', 1, '2026-04-04'),
                ('BT7-064', 1, '2026-04-04'),
                ('BT2-047', 1, '2026-04-04'),
                ('BT3-103', 1, '2026-04-04'),
                ('BT6-100', 1, '2026-04-04'),
                ('EX1-068', 1, '2026-04-04'),
                ('BT7-072', 1, '2026-04-04');

                INSERT INTO BannedPairs (CardNumberA, CardNumberB, EffectiveDate) VALUES
                ('BT20-037', 'BT17-035', '2026-04-04'),
                ('BT20-037', 'EX8-037', '2026-04-04'),
                ('EX2-007', 'EX7-064', '2026-04-04');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BannedPairs");

            migrationBuilder.DropTable(
                name: "CardRestrictions");

            migrationBuilder.DropTable(
                name: "DeckCards");

            migrationBuilder.DropTable(
                name: "Cards");

            migrationBuilder.DropTable(
                name: "Decks");
        }
    }
}
