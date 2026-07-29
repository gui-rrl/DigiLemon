using Microsoft.EntityFrameworkCore;
using RankingDigi.Data;
using RankingDigi.Models;

namespace RankingDigi.Services
{
    /// <summary>
    /// Chaveamento do Top 4 sem lower bracket: semifinais cruzadas (1ºx4º, 2ºx3º) — os vencedores
    /// vão direto pra Grande Final e os perdedores disputam o 3º lugar entre si. Usado só quando
    /// TopCutSize == 4 (ver SwissService.GenerateTopCutAsync); Top 8 e o formato avulso "Dupla
    /// Eliminação" continuam na dupla eliminação completa (DoubleEliminationGenerator).
    ///
    /// Reaproveita a mesma infraestrutura de avanço da dupla eliminação: MatchResultService já
    /// propaga vencedor via NextMatchId e perdedor via LoserGoesToMatchId sem olhar o tipo da
    /// partida, então não precisou mudar nada lá — só a forma como as partidas são criadas aqui.
    /// </summary>
    public class TopFourGenerator
    {
        private readonly RankingContext _context;

        public TopFourGenerator(RankingContext context)
        {
            _context = context;
        }

        /// <param name="seeds">Os 4 classificados, na ordem da classificação: [1º, 2º, 3º, 4º].</param>
        public async Task GenerateAsync(int tournamentId, List<int> seeds)
        {
            if (seeds.Count != 4)
                throw new InvalidOperationException("O Top 4 exige exatamente 4 participantes classificados.");

            // Limpa chaveamento anterior (preserva o histórico Swiss/todos-contra-todos, MatchType 3)
            var old = await _context.TournamentMatches
                .Where(m => m.TournamentId == tournamentId && m.MatchType != 3)
                .ToListAsync();
            _context.TournamentMatches.RemoveRange(old);
            await _context.SaveChangesAsync();

            var semiA = new TournamentMatch { TournamentId = tournamentId, MatchType = 0, Round = 1, Player1Id = seeds[0], Player2Id = seeds[3] }; // 1º x 4º
            var semiB = new TournamentMatch { TournamentId = tournamentId, MatchType = 0, Round = 1, Player1Id = seeds[1], Player2Id = seeds[2] }; // 2º x 3º
            var grandFinal = new TournamentMatch { TournamentId = tournamentId, MatchType = 2, Round = 1 };
            var thirdPlace = new TournamentMatch { TournamentId = tournamentId, MatchType = 4, Round = 1 }; // Disputa de 3º lugar

            _context.TournamentMatches.AddRange(semiA, semiB, grandFinal, thirdPlace);
            await _context.SaveChangesAsync(); // gera os Ids antes de linkar

            semiA.NextMatchId = grandFinal.Id; semiA.NextMatchPosition = 1; semiA.LoserGoesToMatchId = thirdPlace.Id;
            semiB.NextMatchId = grandFinal.Id; semiB.NextMatchPosition = 2; semiB.LoserGoesToMatchId = thirdPlace.Id;

            await _context.SaveChangesAsync();
        }
    }
}
