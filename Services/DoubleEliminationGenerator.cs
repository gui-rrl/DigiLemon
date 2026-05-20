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

            // ---------- 7. Conectar NextMatch no Lower (dinâmico) ----------
            var lowerByRoundDb = lowerDb.GroupBy(m => m.Round).ToDictionary(g => g.Key, g => g.ToList());
            var lowerRoundsOrdered = lowerByRoundDb.Keys.OrderBy(r => r).ToList();
            for (int idx = 0; idx < lowerRoundsOrdered.Count - 1; idx++)
            {
                int currentRoundNum = lowerRoundsOrdered[idx];
                int nextRoundNum = lowerRoundsOrdered[idx + 1];
                var currentRounds = lowerByRoundDb[currentRoundNum];
                var nextRound = lowerByRoundDb[nextRoundNum];
                // Cada partida da rodada atual alimenta uma partida na próxima rodada (agrupando de dois em dois)
                for (int i = 0; i < currentRounds.Count; i++)
                {
                    int nextIdx = i / 2;
                    if (nextIdx < nextRound.Count)
                    {
                        currentRounds[i].NextMatchId = nextRound[nextIdx].Id;
                        currentRounds[i].NextMatchPosition = (i % 2 == 0) ? 1 : 2;
                    }
                }
            }

            // ---------- 8. Associar perdedores do Upper ao Lower ----------
            // Mapeia a rodada do upper para a rodada do lower que recebe os perdedores
            // Rodada 1 upper -> primeira rodada lower
            // Rodada 2 upper -> segunda rodada lower (se existir)
            // Rodada final upper -> última rodada lower
            var lowerRoundsList = lowerRoundsOrdered.ToList();
            for (int roundUpper = 1; roundUpper <= totalRoundsUpper; roundUpper++)
            {
                int lowerRoundIndex;
                if (roundUpper == totalRoundsUpper)
                    lowerRoundIndex = lowerRoundsList.Last();
                else
                    lowerRoundIndex = lowerRoundsList[roundUpper - 1]; // roundUpper-1 index

                if (!lowerByRoundDb.ContainsKey(lowerRoundIndex)) continue;

                var upperRoundMatches = upperByRoundDb[roundUpper];
                var lowerRoundMatches = lowerByRoundDb[lowerRoundIndex];
                for (int i = 0; i < upperRoundMatches.Count; i++)
                {
                    int lowerIdx = i / 2;
                    if (lowerIdx < lowerRoundMatches.Count)
                    {
                        upperRoundMatches[i].LoserGoesToMatchId = lowerRoundMatches[lowerIdx].Id;
                    }
                }
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