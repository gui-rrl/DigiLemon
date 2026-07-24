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

        // Dupla Eliminação / Swiss+Top Cut: campeão = vencedor da Grande Final, vice = perdedor da
        // Grande Final, 3º = perdedor da Final do Lower Bracket (última derrota antes da Grande Final).
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
            int? thirdTpId = lowerFinal?.WinnerId.HasValue == true
                ? (lowerFinal.Player1Id == lowerFinal.WinnerId ? lowerFinal.Player2Id : lowerFinal.Player1Id)
                : null;

            return (championTpId, runnerUpTpId, thirdTpId);
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
