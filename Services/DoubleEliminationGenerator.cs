using Microsoft.EntityFrameworkCore;
using RankingDigi.Data;
using RankingDigi.Models;

namespace RankingDigi.Services
{
    /*
     * Double-Elimination Generator — estrutura para qualquer número par de jogadores.
     *
     * Para N jogadores, arredondamos para a próxima potência de 2 (size) e preenchemos
     * com BYEs (null). Seja U = log2(size) o total de rodadas do Upper Bracket.
     *
     * ── Upper Bracket ────────────────────────────────────────────────────────────
     *   Rodada r  →  size >> r  partidas   (r = 1 … U)
     *   Vencedores avançam para a rodada seguinte (r+1).
     *   Perdedores descem para o Lower Bracket.
     *
     * ── Lower Bracket ────────────────────────────────────────────────────────────
     *   Total de rodadas lower: LRtotal = 2 × (U − 1)
     *
     *   Para a rodada lower lr (1 … LRtotal):
     *     k = (lr + 1) / 2   (divisão inteira)
     *     partidas = size >> (k + 1)
     *
     *   Rodadas ímpar  → rodada "pura lower": os dois jogadores vêm do lower anterior.
     *   Rodadas par    → rodada "drop-in":    um jogador vem do lower anterior (P1)
     *                                          e outro do upper correspondente (P2).
     *
     *   Mapeamento Upper → Lower (quem cai em qual rodada lower):
     *     UR1 losers  →  LR1  (2 losers por partida lower — emparelhados)
     *     UR(k+1)     →  LR(2k)   para k = 1 … U-1
     *
     * ── Grand Final ──────────────────────────────────────────────────────────────
     *   Vencedor do Upper Final (P1) vs Vencedor do Lower Final (P2).
     *
     * ── Convenção de slots ───────────────────────────────────────────────────────
     *   NextMatchPosition = 1  →  Player1Id da próxima partida  (vencedor do lower)
     *   NextMatchPosition = 2  →  Player2Id da próxima partida  (vencedor do upper / drop-in)
     *   LoserGoesToMatchId     →  preenchido pelo SetMatchResult: P2Id primeiro, depois P1Id.
     */
    public class DoubleEliminationGenerator
    {
        private readonly RankingContext _context;

        public DoubleEliminationGenerator(RankingContext context)
        {
            _context = context;
        }

        public async Task GenerateAsync(int tournamentId, List<int> playerIds)
        {
            // ── Limpar chaveamento anterior ───────────────────────────────────────
            var old = await _context.TournamentMatches
                .Where(m => m.TournamentId == tournamentId)
                .ToListAsync();
            _context.TournamentMatches.RemoveRange(old);
            await _context.SaveChangesAsync();

            int size = NextPowerOfTwo(playerIds.Count);
            var slots = playerIds.Select(p => (int?)p).ToList();
            while (slots.Count < size) slots.Add(null);
            slots = Shuffle(slots);

            int U       = (int)Math.Log2(size); // rodadas do upper
            int totalLR = 2 * (U - 1);          // rodadas do lower

            // ── 1. Criar partidas Upper ──────────────────────────────────────────
            var upper = new List<TournamentMatch>();
            for (int r = 1; r <= U; r++)
            {
                int count = size >> r;
                for (int i = 0; i < count; i++)
                {
                    var m = new TournamentMatch
                    {
                        TournamentId = tournamentId,
                        MatchType    = 0,   // Upper
                        Round        = r,
                    };
                    if (r == 1)
                    {
                        m.Player1Id = slots[i * 2];
                        m.Player2Id = slots[i * 2 + 1];
                    }
                    upper.Add(m);
                }
            }

            // ── 2. Criar partidas Lower ──────────────────────────────────────────
            // lr ímpar  → rodada pura lower (emparelhamento dos sobreviventes)
            // lr par    → rodada drop-in    (sobrevivente lower vs perdedor upper)
            //
            // Contagem por rodada lr:
            //   k = (lr + 1) / 2  →  partidas = size >> (k + 1)
            var lower = new List<TournamentMatch>();
            for (int lr = 1; lr <= totalLR; lr++)
            {
                int k     = (lr + 1) / 2;
                int count = size >> (k + 1);
                for (int i = 0; i < count; i++)
                {
                    lower.Add(new TournamentMatch
                    {
                        TournamentId = tournamentId,
                        MatchType    = 1,   // Lower
                        Round        = lr,
                    });
                }
            }

            // ── 3. Grande Final ──────────────────────────────────────────────────
            var grandFinal = new TournamentMatch
            {
                TournamentId = tournamentId,
                MatchType    = 2,   // Grand Final
                Round        = 1,
            };

            _context.TournamentMatches.AddRange(upper);
            _context.TournamentMatches.AddRange(lower);
            _context.TournamentMatches.Add(grandFinal);
            await _context.SaveChangesAsync();

            // ── 4. Recarregar com IDs gerados pelo banco ─────────────────────────
            var all = await _context.TournamentMatches
                .Where(m => m.TournamentId == tournamentId)
                .ToListAsync();

            var uByR = all
                .Where(m => m.MatchType == 0)
                .GroupBy(m => m.Round)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Id).ToList());

            var lByR = all
                .Where(m => m.MatchType == 1)
                .GroupBy(m => m.Round)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Id).ToList());

            var final = all.First(m => m.MatchType == 2);

            // ── 5. Ligar NextMatchId no Upper ────────────────────────────────────
            // Cada par de partidas da rodada r avança para 1 partida da rodada r+1.
            for (int r = 1; r < U; r++)
            {
                var curr = uByR[r];
                var next = uByR[r + 1];
                for (int i = 0; i < curr.Count; i++)
                {
                    curr[i].NextMatchId       = next[i / 2].Id;
                    curr[i].NextMatchPosition = (i % 2 == 0) ? 1 : 2;
                }
            }
            // Upper Final → Grand Final (P1)
            uByR[U][0].NextMatchId       = final.Id;
            uByR[U][0].NextMatchPosition = 1;

            // ── 6. Ligar NextMatchId no Lower ────────────────────────────────────
            // lr ímpar → lr+1 : mesma quantidade → 1:1 (vencedor vira P1)
            // lr par   → lr+1 : dobro de partidas → emparelhar (P1 / P2)
            for (int lr = 1; lr < totalLR; lr++)
            {
                var curr = lByR[lr];
                var next = lByR[lr + 1];

                if (curr.Count == next.Count)
                {
                    // Rodada ímpar → par : 1:1, vencedor vai para P1 da próxima
                    for (int i = 0; i < curr.Count; i++)
                    {
                        curr[i].NextMatchId       = next[i].Id;
                        curr[i].NextMatchPosition = 1;
                    }
                }
                else
                {
                    // Rodada par → ímpar : 2:1, emparelhar (P1 e P2)
                    for (int i = 0; i < curr.Count; i++)
                    {
                        curr[i].NextMatchId       = next[i / 2].Id;
                        curr[i].NextMatchPosition = (i % 2 == 0) ? 1 : 2;
                    }
                }
            }
            // Lower Final → Grand Final (P2)
            lByR[totalLR][0].NextMatchId       = final.Id;
            lByR[totalLR][0].NextMatchPosition = 2;

            // ── 7. Perdedores do Upper → Lower ───────────────────────────────────
            //
            // UR1 : cada par de partidas upper alimenta 1 partida de LR1
            //        UR1[0], UR1[1] → LR1[0]
            //        UR1[2], UR1[3] → LR1[1]   etc.
            var ur1 = uByR[1];
            var lr1 = lByR[1];
            for (int i = 0; i < ur1.Count; i++)
                ur1[i].LoserGoesToMatchId = lr1[i / 2].Id;

            // UR(k+1) → LR(2k)  para k = 1 … U-1  (1:1, perdedor vira P2 via controller)
            for (int k = 1; k <= U - 1; k++)
            {
                var urRound = uByR[k + 1];
                var lrRound = lByR[2 * k];
                for (int i = 0; i < urRound.Count; i++)
                    urRound[i].LoserGoesToMatchId = lrRound[i].Id;
            }

            await _context.SaveChangesAsync();

            // ── 8. Resolver BYEs automaticamente ────────────────────────────────
            await ResolveByes(tournamentId);
        }

        // ── Auxiliares ────────────────────────────────────────────────────────────

        private static int NextPowerOfTwo(int n)
        {
            int p = 1;
            while (p < n) p <<= 1;
            return p;
        }

        private static List<int?> Shuffle(List<int?> list)
        {
            var rnd = new Random();
            return list.OrderBy(_ => rnd.Next()).ToList();
        }

        private async Task ResolveByes(int tournamentId)
        {
            var all = await _context.TournamentMatches
                .Where(m => m.TournamentId == tournamentId)
                .ToListAsync();

            bool changed;
            do
            {
                changed = false;
                foreach (var match in all)
                {
                    if (match.IsPlayed) continue;

                    bool hasP1 = match.Player1Id.HasValue;
                    bool hasP2 = match.Player2Id.HasValue;

                    int? winnerId = null;
                    bool resolve  = false;

                    if      (hasP1 && !hasP2) { winnerId = match.Player1Id; resolve = true; }
                    else if (!hasP1 && hasP2) { winnerId = match.Player2Id; resolve = true; }
                    // Ambos null = partida futura aguardando jogadores → não tocar.

                    if (!resolve) continue;

                    match.WinnerId = winnerId;
                    match.IsPlayed = true;
                    match.Date     = DateTime.UtcNow;
                    changed        = true;

                    // Propaga vencedor para próxima partida
                    if (winnerId.HasValue && match.NextMatchId.HasValue)
                    {
                        var next = all.FirstOrDefault(m => m.Id == match.NextMatchId);
                        if (next != null)
                        {
                            if (match.NextMatchPosition == 1) next.Player1Id = winnerId;
                            else                              next.Player2Id = winnerId;
                        }
                    }

                    // Perdedor nulo não precisa ser propagado ao lower
                    // (o slot fica null e a partida lower também resolverá como BYE)
                }
            }
            while (changed);

            await _context.SaveChangesAsync();
        }
    }
}
