using Microsoft.EntityFrameworkCore;
using RankingDigi.Data;

namespace RankingDigi.Services
{
    /// <summary>
    /// Regra de rotação de decks: os 4 decks que disputaram o Top Cut de 4 (Grande Final +
    /// disputa de 3º lugar) do último torneio finalizado ficam banidos de entrar no próximo
    /// torneio. "Próximo" aqui não é um torneio específico marcado antecipadamente — é sempre
    /// relativo ao torneio finalizado mais recente: assim que outro torneio termina, o
    /// banimento passa a ser sobre o Top 4 dele, substituindo o anterior.
    ///
    /// Só vale pra torneios com TopCutSize == 4 (Swiss+Top Cut ou Todos contra todos+Top Cut):
    /// é o único formato em que o app sabe com precisão quem ficou em 1º/2º/3º/4º (via
    /// TopFourGenerator). No Top 8 o 4º lugar não é rastreado, então a regra não se aplica lá.
    /// </summary>
    public static class DeckBanService
    {
        public static async Task<HashSet<int>> GetBannedDeckIdsAsync(RankingContext context)
        {
            var lastTopFour = await context.Tournaments
                .Where(t => t.Status == 2 && t.TopCutSize == 4 && (t.Format == 1 || t.Format == 3))
                .OrderByDescending(t => t.Id)
                .FirstOrDefaultAsync();

            if (lastTopFour == null) return new HashSet<int>();

            var grandFinal = await context.TournamentMatches
                .FirstOrDefaultAsync(m => m.TournamentId == lastTopFour.Id && m.MatchType == 2);
            var thirdPlaceMatch = await context.TournamentMatches
                .FirstOrDefaultAsync(m => m.TournamentId == lastTopFour.Id && m.MatchType == 4);

            // Top Cut não chegou a ser decidido (Grande Final ou disputa de 3º sem vencedor) — nada pra banir.
            if (grandFinal?.WinnerId == null || thirdPlaceMatch?.WinnerId == null)
                return new HashSet<int>();

            var topFourTpIds = new[]
            {
                grandFinal.WinnerId,
                grandFinal.Player1Id == grandFinal.WinnerId ? grandFinal.Player2Id : grandFinal.Player1Id,
                thirdPlaceMatch.WinnerId,
                thirdPlaceMatch.Player1Id == thirdPlaceMatch.WinnerId ? thirdPlaceMatch.Player2Id : thirdPlaceMatch.Player1Id,
            }
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();

            var deckIds = await context.TournamentPlayers
                .Where(tp => topFourTpIds.Contains(tp.Id) && tp.DeckId.HasValue)
                .Select(tp => tp.DeckId!.Value)
                .ToListAsync();

            return deckIds.ToHashSet();
        }
    }
}
