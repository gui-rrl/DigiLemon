using Microsoft.EntityFrameworkCore;
using RankingDigi.Data;
using RankingDigi.Models;

namespace RankingDigi.Services
{
    public class DoubleEliminationGenerator
    {
        private readonly RankingContext _context;

        public DoubleEliminationGenerator(RankingContext context)
        {
            _context = context;
        }

        public async Task GenerateAsync(int tournamentId, List<int> playerIds)
        {
            // Remove chaveamento anterior
            var oldMatches = await _context.TournamentMatches
                .Where(m => m.TournamentId == tournamentId)
                .ToListAsync();
            _context.TournamentMatches.RemoveRange(oldMatches);
            await _context.SaveChangesAsync();

            int size = GetNextPowerOfTwo(playerIds.Count);
            var slots = new List<int?>(playerIds.Select(p => (int?)p));
            while (slots.Count < size) slots.Add(null);
            slots = Shuffle(slots);

            // ---------- 1. Upper bracket ----------
            int totalRoundsUpper = (int)Math.Log2(size);
            var upperMatches = new List<TournamentMatch>();
            var upperByRound = new Dictionary<int, List<TournamentMatch>>();

            for (int round = 1; round <= totalRoundsUpper; round++)
            {
                int matchesInRound = size / (int)Math.Pow(2, round);
                var roundMatches = new List<TournamentMatch>();
                for (int i = 0; i < matchesInRound; i++)
                {
                    var match = new TournamentMatch
                    {
                        TournamentId = tournamentId,
                        MatchType = 0,
                        Round = round,
                        IsPlayed = false
                    };
                    if (round == 1)
                    {
                        match.Player1Id = slots[i * 2];
                        match.Player2Id = slots[i * 2 + 1];
                    }
                    roundMatches.Add(match);
                }
                upperMatches.AddRange(roundMatches);
                upperByRound[round] = roundMatches;
            }

            // ---------- 2. Lower bracket (dinâmico) ----------
            // Total de partidas lower = size - 2 (para dupla eliminação sem partida extra)
            int totalLowerMatches = size - 2;
            var lowerMatches = new List<TournamentMatch>();

            // Distribui as partidas lower por rodadas
            // Exemplo: size=4 -> rodadas: R1:1, R2:1 (total 2)
            // size=8 -> rodadas: R1:2, R2:2, R3:1, R4:1 (total 6)
            // size=16 -> rodadas: R1:4, R2:4, R3:2, R4:2, R5:1, R6:1 (total 14)
            var lowerRounds = new List<int>();
            int matchesLeft = totalLowerMatches;
            int currentRound = 1;
            while (matchesLeft > 0)
            {
                int matchesThisRound = Math.Min(size / (int)Math.Pow(2, (currentRound + 1) / 2 + 1), matchesLeft);
                if (matchesThisRound < 1) matchesThisRound = 1;
                for (int i = 0; i < matchesThisRound; i++)
                {
                    lowerRounds.Add(currentRound);
                    matchesLeft--;
                }
                currentRound++;
            }

            for (int i = 0; i < totalLowerMatches; i++)
            {
                var match = new TournamentMatch
                {
                    TournamentId = tournamentId,
                    MatchType = 1,
                    Round = lowerRounds[i],
                    IsPlayed = false
                };
                lowerMatches.Add(match);
            }

            // ---------- 3. Grande final ----------
            var grandFinal = new TournamentMatch
            {
                TournamentId = tournamentId,
                MatchType = 2,
                Round = 1,
                IsPlayed = false
            };

            // ---------- 4. Persistir ----------
            _context.TournamentMatches.AddRange(upperMatches);
            _context.TournamentMatches.AddRange(lowerMatches);
            _context.TournamentMatches.Add(grandFinal);
            await _context.SaveChangesAsync();

            // ---------- 5. Recarregar com IDs ----------
            var allMatchesDb = await _context.TournamentMatches
                .Where(m => m.TournamentId == tournamentId)
                .ToListAsync();

            var upperDb = allMatchesDb.Where(m => m.MatchType == 0).OrderBy(m => m.Round).ThenBy(m => m.Id).ToList();
            var lowerDb = allMatchesDb.Where(m => m.MatchType == 1).OrderBy(m => m.Round).ThenBy(m => m.Id).ToList();
            var finalDb = allMatchesDb.First(m => m.MatchType == 2);

            var upperByRoundDb = upperDb.GroupBy(m => m.Round).ToDictionary(g => g.Key, g => g.ToList());

            // ---------- 6. Conectar NextMatch no Upper ----------
            for (int round = 1; round < totalRoundsUpper; round++)
            {
                var current = upperByRoundDb[round];
                var next = upperByRoundDb[round + 1];
                for (int i = 0; i < current.Count; i++)
                {
                    int nextIdx = i / 2;
                    current[i].NextMatchId = next[nextIdx].Id;
                    current[i].NextMatchPosition = (i % 2 == 0) ? 1 : 2;
                }
            }

            // ---------- 7. Conectar NextMatch no Lower (correto para dupla eliminação) ----------
            var lowerByRoundDb = lowerDb.GroupBy(m => m.Round).ToDictionary(g => g.Key, g => g.ToList());
            var lowerRoundsOrdered = lowerByRoundDb.Keys.OrderBy(r => r).ToList();

            for (int idx = 0; idx < lowerRoundsOrdered.Count - 1; idx++)
            {
                int currentRoundNum = lowerRoundsOrdered[idx];
                int nextRoundNum = lowerRoundsOrdered[idx + 1];
                var currRoundMatches = lowerByRoundDb[currentRoundNum];
                var nextRoundMatches = lowerByRoundDb[nextRoundNum];

                // Caso 1: mesmo número de partidas (ex: rodada 1 -> rodada 2, com 2 partidas cada)
                if (currRoundMatches.Count == nextRoundMatches.Count)
                {
                    for (int i = 0; i < currRoundMatches.Count; i++)
                    {
                        currRoundMatches[i].NextMatchId = nextRoundMatches[i].Id;
                        // Vencedor da lower vai sempre para o primeiro slot (Player1Id)
                        currRoundMatches[i].NextMatchPosition = 1;
                    }
                }
                // Caso 2: curr tem o dobro de partidas (ex: rodada 2 -> rodada 3, 2 partidas para 1)
                else if (currRoundMatches.Count == nextRoundMatches.Count * 2)
                {
                    for (int i = 0; i < currRoundMatches.Count; i++)
                    {
                        int nextIdx = i / 2;
                        currRoundMatches[i].NextMatchId = nextRoundMatches[nextIdx].Id;
                        // A primeira partida do par alimenta Player1Id, a segunda alimenta Player2Id
                        currRoundMatches[i].NextMatchPosition = (i % 2 == 0) ? 1 : 2;
                    }
                }
                // Caso genérico (fallback)
                else
                {
                    for (int i = 0; i < currRoundMatches.Count; i++)
                    {
                        int nextIdx = i * nextRoundMatches.Count / currRoundMatches.Count;
                        currRoundMatches[i].NextMatchId = nextRoundMatches[nextIdx].Id;
                        currRoundMatches[i].NextMatchPosition = 1;
                    }
                }
            }

            // ---------- 8. Associar perdedores do Upper ao Lower (CORRIGIDO) ----------
            var upperAll = upperDb.OrderBy(m => m.Round).ThenBy(m => m.Id).ToList();
            var lowerAll = lowerDb.OrderBy(m => m.Round).ThenBy(m => m.Id).ToList();

            // Rodada 1: 4 partidas upper -> 2 partidas lower (dois perdedores por partida lower)
            var upperRound1 = upperAll.Where(m => m.Round == 1).OrderBy(m => m.Id).ToList();
            var lowerRound1 = lowerAll.Where(m => m.Round == 1).OrderBy(m => m.Id).ToList();
            int lowerIdx = 0;
            for (int i = 0; i < upperRound1.Count; i++)
            {
                upperRound1[i].LoserGoesToMatchId = lowerRound1[lowerIdx].Id;
                if ((i + 1) % 2 == 0) lowerIdx++;
            }

            // Rodada 2: 2 partidas upper -> 2 partidas lower (um perdedor por partida lower, mapeamento direto 1:1)
            var upperRound2 = upperAll.Where(m => m.Round == 2).OrderBy(m => m.Id).ToList();
            var lowerRound2 = lowerAll.Where(m => m.Round == 2).OrderBy(m => m.Id).ToList();
            if (upperRound2.Count == lowerRound2.Count)
            {
                for (int i = 0; i < upperRound2.Count; i++)
                {
                    upperRound2[i].LoserGoesToMatchId = lowerRound2[i].Id;
                }
            }
            else
            {
                // Fallback: usar índices sequenciais
                for (int i = 0; i < upperRound2.Count; i++)
                {
                    if (i < lowerRound2.Count)
                        upperRound2[i].LoserGoesToMatchId = lowerRound2[i].Id;
                }
            }

            // Rodada 3: 1 partida upper -> última partida lower (rodada mais alta)
            var upperRound3 = upperAll.Where(m => m.Round == 3).OrderBy(m => m.Id).ToList();
            if (upperRound3.Any())
            {
                var lastLower = lowerAll.Last();
                upperRound3[0].LoserGoesToMatchId = lastLower.Id;
            }

            // ---------- 9. Conectar finais à Grande Final ----------
            var upperFinal = upperByRoundDb[totalRoundsUpper].First();
            upperFinal.NextMatchId = finalDb.Id;
            upperFinal.NextMatchPosition = 1;

            var lowerFinal = lowerDb.Last();
            lowerFinal.NextMatchId = finalDb.Id;
            lowerFinal.NextMatchPosition = 2;

            await _context.SaveChangesAsync();

            // ---------- 10. Resolver byes ----------
            await ResolveByes(tournamentId);
        }

        // ----------------------- MÉTODOS AUXILIARES -----------------------
        private int GetNextPowerOfTwo(int n)
        {
            int p = 1;
            while (p < n) p <<= 1;
            return p;
        }

        private List<int?> Shuffle(List<int?> list)
        {
            var rnd = new Random();
            return list.OrderBy(x => rnd.Next()).ToList();
        }

        private async Task ResolveByes(int tournamentId)
        {
            var allMatches = await _context.TournamentMatches
                .Where(m => m.TournamentId == tournamentId)
                .ToListAsync();

            bool changed;
            do
            {
                changed = false;
                foreach (var match in allMatches)
                {
                    if (match.IsPlayed) continue;

                    bool hasP1 = match.Player1Id.HasValue;
                    bool hasP2 = match.Player2Id.HasValue;

                    if (hasP1 && !hasP2)
                    {
                        match.WinnerId = match.Player1Id;
                        match.IsPlayed = true;
                        match.Date = DateTime.UtcNow;
                        changed = true;

                        if (match.NextMatchId.HasValue)
                        {
                            var nextMatch = allMatches.FirstOrDefault(m => m.Id == match.NextMatchId);
                            if (nextMatch != null)
                            {
                                if (match.NextMatchPosition == 1)
                                    nextMatch.Player1Id = match.WinnerId;
                                else
                                    nextMatch.Player2Id = match.WinnerId;
                            }
                        }
                    }
                    else if (!hasP1 && hasP2)
                    {
                        match.WinnerId = match.Player2Id;
                        match.IsPlayed = true;
                        match.Date = DateTime.UtcNow;
                        changed = true;

                        if (match.NextMatchId.HasValue)
                        {
                            var nextMatch = allMatches.FirstOrDefault(m => m.Id == match.NextMatchId);
                            if (nextMatch != null)
                            {
                                if (match.NextMatchPosition == 1)
                                    nextMatch.Player1Id = match.WinnerId;
                                else
                                    nextMatch.Player2Id = match.WinnerId;
                            }
                        }
                    }
                }
            } while (changed);

            await _context.SaveChangesAsync();
        }
    }
}