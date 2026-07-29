using Microsoft.EntityFrameworkCore;
using RankingDigi.Data;
using RankingDigi.Models;

namespace RankingDigi.Services
{
    // Premia o ranking geral (Player.Score/CareerScore, separado por modalidade) com base
    // nos resultados de partidas de torneio e na colocação final, para qualquer formato.
    public static class TournamentScoringService
    {
        private const int WinPoints  = 3;
        private const int DrawPoints = 1;
        private static readonly int[] PlacementBonus = { 10, 7, 4 }; // campeão, vice, 3º lugar

        private static void AddPoints(Player player, bool online, int amount)
        {
            if (online) { player.ScoreOnline += amount; player.CareerScoreOnline += amount; }
            else        { player.Score += amount;       player.CareerScore += amount; }
        }

        // Trava em 0: pontuação negativa no ranking não quer dizer nada, e chegar em negativo
        // aqui significaria que os pontos já tinham sido mexidos por fora deste serviço.
        private static void RemovePoints(Player player, bool online, int amount)
        {
            if (online)
            {
                player.ScoreOnline       = Math.Max(0, player.ScoreOnline - amount);
                player.CareerScoreOnline = Math.Max(0, player.CareerScoreOnline - amount);
            }
            else
            {
                player.Score       = Math.Max(0, player.Score - amount);
                player.CareerScore = Math.Max(0, player.CareerScore - amount);
            }
        }

        // Vitória = 3 pts, derrota = 0, empate = 1 pt pra cada (empate só é possível em rodada Swiss).
        public static async Task AwardMatchResultAsync(RankingContext context, TournamentMatch match, Tournament tournament)
        {
            if (match.IsBye || !match.IsPlayed) return;
            if (!match.Player1Id.HasValue || !match.Player2Id.HasValue) return;

            bool online = tournament.Mode == 1;

            var tp1 = await context.TournamentPlayers.FindAsync(match.Player1Id.Value);
            var tp2 = await context.TournamentPlayers.FindAsync(match.Player2Id.Value);
            var player1 = tp1?.PlayerId.HasValue == true ? await context.Players.FindAsync(tp1.PlayerId!.Value) : null;
            var player2 = tp2?.PlayerId.HasValue == true ? await context.Players.FindAsync(tp2.PlayerId!.Value) : null;

            if (!match.WinnerId.HasValue)
            {
                // Empate: só chega aqui vindo de rodada Swiss (bracket sempre tem vencedor).
                if (player1 != null) AddPoints(player1, online, DrawPoints);
                if (player2 != null) AddPoints(player2, online, DrawPoints);
                return;
            }

            Player? winnerPlayer = match.WinnerId.Value == tp1?.Id ? player1
                                  : match.WinnerId.Value == tp2?.Id ? player2
                                  : null;
            if (winnerPlayer != null) AddPoints(winnerPlayer, online, WinPoints);
        }

        // Dupla Eliminação / Swiss+Top Cut / Todos contra todos+Top Cut: campeão = vencedor da
        // Grande Final, vice = perdedor da Grande Final. O 3º lugar depende do formato do chaveamento:
        //   - Top 8 (ou o torneio avulso "Dupla Eliminação"): perdedor da Final do Lower Bracket
        //     (última derrota antes da Grande Final).
        //   - Top 4 (TopFourGenerator, sem lower bracket): vencedor da partida dedicada de
        //     disputa de 3º lugar (MatchType 4), entre os dois perdedores das semifinais.
        // Além do 3º, a estrutura de bracket não dá uma ordem limpa pros demais (empate técnico por rodada de eliminação).
        public static async Task<(int? Champion, int? RunnerUp, int? Third)> GetBracketPlacementsAsync(RankingContext context, int tournamentId)
        {
            var grandFinal = await context.TournamentMatches
                .FirstOrDefaultAsync(m => m.TournamentId == tournamentId && m.MatchType == 2);
            if (grandFinal?.WinnerId == null) return (null, null, null);

            int championTpId  = grandFinal.WinnerId.Value;
            int? runnerUpTpId = grandFinal.Player1Id == championTpId ? grandFinal.Player2Id : grandFinal.Player1Id;

            var lowerFinal = await context.TournamentMatches.FirstOrDefaultAsync(m =>
                m.TournamentId == tournamentId && m.MatchType == 1 && m.NextMatchId == grandFinal.Id);
            if (lowerFinal != null)
            {
                int? thirdViaLower = lowerFinal.WinnerId.HasValue
                    ? (lowerFinal.Player1Id == lowerFinal.WinnerId ? lowerFinal.Player2Id : lowerFinal.Player1Id)
                    : null;
                return (championTpId, runnerUpTpId, thirdViaLower);
            }

            var thirdPlaceMatch = await context.TournamentMatches
                .FirstOrDefaultAsync(m => m.TournamentId == tournamentId && m.MatchType == 4);
            return (championTpId, runnerUpTpId, thirdPlaceMatch?.WinnerId);
        }

        public static async Task AwardBracketPlacementBonusAsync(RankingContext context, Tournament tournament)
        {
            var (champion, runnerUp, third) = await GetBracketPlacementsAsync(context, tournament.Id);
            if (!champion.HasValue) return;

            await AwardPlacementsAsync(context, tournament, new[] { champion, runnerUp, third });
        }

        // Swiss Pontos Corridos: top 3 da classificação final (pontos, depois OMW%, depois vitórias).
        public static async Task AwardSwissStandingsPlacementBonusAsync(RankingContext context, Tournament tournament, List<SwissStandingEntry> standings)
        {
            bool online = tournament.Mode == 1;
            for (int i = 0; i < standings.Count && i < PlacementBonus.Length; i++)
            {
                if (!standings[i].PlayerId.HasValue) continue;
                var player = await context.Players.FindAsync(standings[i].PlayerId!.Value);
                if (player != null) AddPoints(player, online, PlacementBonus[i]);
            }
        }

        /// <summary>
        /// Desfaz no ranking geral tudo que este torneio creditou: pontos de partida e, se ele
        /// chegou a ser finalizado, o bônus de colocação. Precisa rodar ANTES de apagar as
        /// partidas/participantes, porque é deles que os valores são recalculados.
        ///
        /// Existe porque excluir um torneio deixava a pontuação inflada para sempre — quem
        /// tivesse vencido nele continuava com os pontos, sem nenhum registro que explicasse.
        /// </summary>
        /// <param name="swissStandings">
        /// Classificação final, obrigatória apenas para Swiss Pontos Corridos finalizado (é dela
        /// que saiu o bônus). O chamador passa porque quem calcula é o SwissService.
        /// </param>
        public static async Task RevertTournamentAsync(RankingContext context, Tournament tournament, List<SwissStandingEntry>? swissStandings = null)
        {
            bool online = tournament.Mode == 1;

            // ── 1) Pontos de partida (espelha AwardMatchResultAsync) ──────────────────
            var matches = await context.TournamentMatches
                .Where(m => m.TournamentId == tournament.Id && m.IsPlayed && !m.IsBye)
                .ToListAsync();

            foreach (var match in matches)
            {
                if (!match.Player1Id.HasValue || !match.Player2Id.HasValue) continue;

                var tp1 = await context.TournamentPlayers.FindAsync(match.Player1Id.Value);
                var tp2 = await context.TournamentPlayers.FindAsync(match.Player2Id.Value);
                var player1 = tp1?.PlayerId.HasValue == true ? await context.Players.FindAsync(tp1.PlayerId!.Value) : null;
                var player2 = tp2?.PlayerId.HasValue == true ? await context.Players.FindAsync(tp2.PlayerId!.Value) : null;

                if (!match.WinnerId.HasValue)
                {
                    if (player1 != null) RemovePoints(player1, online, DrawPoints);
                    if (player2 != null) RemovePoints(player2, online, DrawPoints);
                    continue;
                }

                Player? winnerPlayer = match.WinnerId.Value == tp1?.Id ? player1
                                      : match.WinnerId.Value == tp2?.Id ? player2
                                      : null;
                if (winnerPlayer != null) RemovePoints(winnerPlayer, online, WinPoints);
            }

            // ── 2) Bônus de colocação (só existe em torneio finalizado) ───────────────
            if (tournament.Status != 2) return;

            if (tournament.Format == 2)
            {
                // Swiss Pontos Corridos: bônus veio da classificação final.
                if (swissStandings == null) return;
                for (int i = 0; i < swissStandings.Count && i < PlacementBonus.Length; i++)
                {
                    if (!swissStandings[i].PlayerId.HasValue) continue;
                    var player = await context.Players.FindAsync(swissStandings[i].PlayerId!.Value);
                    if (player != null) RemovePoints(player, online, PlacementBonus[i]);
                }
                return;
            }

            // Dupla Eliminação / Swiss+Top Cut: bônus veio do resultado da Grande Final.
            var (champion, runnerUp, third) = await GetBracketPlacementsAsync(context, tournament.Id);
            if (!champion.HasValue) return;

            var ordem = new[] { champion, runnerUp, third };
            for (int i = 0; i < ordem.Length && i < PlacementBonus.Length; i++)
            {
                if (!ordem[i].HasValue) continue;
                var tp = await context.TournamentPlayers.FindAsync(ordem[i]!.Value);
                if (tp?.PlayerId == null) continue;
                var player = await context.Players.FindAsync(tp.PlayerId.Value);
                if (player != null) RemovePoints(player, online, PlacementBonus[i]);
            }
        }

        private static async Task AwardPlacementsAsync(RankingContext context, Tournament tournament, int?[] tpIdsInOrder)
        {
            bool online = tournament.Mode == 1;
            for (int i = 0; i < tpIdsInOrder.Length && i < PlacementBonus.Length; i++)
            {
                if (!tpIdsInOrder[i].HasValue) continue;
                var tp = await context.TournamentPlayers.FindAsync(tpIdsInOrder[i]!.Value);
                if (tp?.PlayerId == null) continue;
                var player = await context.Players.FindAsync(tp.PlayerId.Value);
                if (player != null) AddPoints(player, online, PlacementBonus[i]);
            }
        }
    }
}
