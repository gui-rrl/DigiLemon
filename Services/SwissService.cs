using Microsoft.EntityFrameworkCore;
using RankingDigi.Data;
using RankingDigi.Models;

namespace RankingDigi.Services
{
    // MatchType = 3 → Swiss
    // MatchType = 0/1/2 → Top Cut (double elimination, reutiliza DoubleEliminationGenerator)
    public class SwissService
    {
        private readonly RankingContext _context;

        public SwissService(RankingContext context) => _context = context;

        // ── Fórmula padrão de rodadas Swiss ──────────────────────────────────
        public static int CalculateRounds(int playerCount) => playerCount switch
        {
            <= 2  => 1,
            <= 4  => 2,
            <= 8  => 3,
            <= 16 => 4,
            <= 32 => 5,
            _     => 6,
        };

        // ── Iniciar: gera a rodada 1 ─────────────────────────────────────────
        public async Task StartAsync(int tournamentId)
        {
            var tournament = await _context.Tournaments.FindAsync(tournamentId)
                ?? throw new InvalidOperationException("Torneio não encontrado.");

            if (tournament.CurrentSwissRound != 0)
                throw new InvalidOperationException("O Swiss já foi iniciado.");

            var tps = await _context.TournamentPlayers
                .Where(tp => tp.TournamentId == tournamentId)
                .ToListAsync();

            if (tps.Count < 2)
                throw new InvalidOperationException("Mínimo de 2 participantes para iniciar.");

            // Garante que rounds foi calculado
            if (tournament.SwissRounds == 0)
                tournament.SwissRounds = CalculateRounds(tps.Count);

            tournament.Status = 1;
            tournament.CurrentSwissRound = 1;
            await _context.SaveChangesAsync();

            await GenerateRoundAsync(tournamentId, 1, tps.Select(tp => tp.Id).ToList());
        }

        // ── Avançar para a próxima rodada ────────────────────────────────────
        public async Task AdvanceRoundAsync(int tournamentId)
        {
            var tournament = await _context.Tournaments.FindAsync(tournamentId)
                ?? throw new InvalidOperationException("Torneio não encontrado.");

            int currentRound = tournament.CurrentSwissRound;
            if (currentRound == 0)
                throw new InvalidOperationException("Swiss não foi iniciado.");

            var roundMatches = await _context.TournamentMatches
                .Where(m => m.TournamentId == tournamentId && m.MatchType == 3 && m.Round == currentRound)
                .ToListAsync();

            if (roundMatches.Any(m => !m.IsPlayed))
                throw new InvalidOperationException("Todas as partidas da rodada atual precisam ser finalizadas antes de avançar.");

            if (currentRound >= tournament.SwissRounds)
                throw new InvalidOperationException("Todas as rodadas Swiss já foram concluídas. Gere o Top Cut.");

            tournament.CurrentSwissRound++;
            await _context.SaveChangesAsync();

            var tpIds = await _context.TournamentPlayers
                .Where(tp => tp.TournamentId == tournamentId)
                .Select(tp => tp.Id)
                .ToListAsync();

            await GenerateRoundAsync(tournamentId, tournament.CurrentSwissRound, tpIds);
        }

        // ── Gerar Top Cut (double elimination com os top N) ──────────────────
        public async Task GenerateTopCutAsync(int tournamentId)
        {
            var tournament = await _context.Tournaments.FindAsync(tournamentId)
                ?? throw new InvalidOperationException("Torneio não encontrado.");

            if (tournament.CurrentSwissRound < tournament.SwissRounds)
                throw new InvalidOperationException("O Swiss ainda não terminou.");

            var lastRoundMatches = await _context.TournamentMatches
                .Where(m => m.TournamentId == tournamentId && m.MatchType == 3 && m.Round == tournament.SwissRounds)
                .ToListAsync();

            if (lastRoundMatches.Any(m => !m.IsPlayed))
                throw new InvalidOperationException("Finalize todas as partidas da última rodada antes de gerar o Top Cut.");

            int topN = tournament.TopCutSize;
            var standings = await GetStandingsRawAsync(tournamentId);
            var topPlayers = standings.Take(topN).Select(s => s.TpId).ToList();

            if (topPlayers.Count < 2)
                throw new InvalidOperationException("Não há jogadores suficientes para o Top Cut.");

            var generator = new DoubleEliminationGenerator(_context);
            await generator.GenerateAsync(tournamentId, topPlayers);
        }

        // ── Standings ────────────────────────────────────────────────────────
        public async Task<List<SwissStandingEntry>> GetStandingsAsync(int tournamentId)
            => await GetStandingsRawAsync(tournamentId);

        // ── Internos ─────────────────────────────────────────────────────────

        private async Task GenerateRoundAsync(int tournamentId, int round, List<int> allTpIds)
        {
            // Carrega pontuações atuais
            var tps = await _context.TournamentPlayers
                .Where(tp => tp.TournamentId == tournamentId)
                .ToListAsync();

            // Carrega histórico de confrontos para evitar rematches
            var history = await _context.TournamentMatches
                .Where(m => m.TournamentId == tournamentId && m.MatchType == 3 && m.Round < round)
                .ToListAsync();

            var played = new HashSet<(int, int)>(
                history
                    .Where(m => m.Player1Id.HasValue && m.Player2Id.HasValue)
                    .Select(m => (Math.Min(m.Player1Id!.Value, m.Player2Id!.Value),
                                  Math.Max(m.Player1Id!.Value, m.Player2Id!.Value)))
            );

            // Ordena por pontos DESC, vitórias DESC, aleatório como desempate
            var rng = new Random();
            var sorted = tps
                .OrderByDescending(tp => tp.SwissPoints)
                .ThenByDescending(tp => tp.SwissWins)
                .ThenBy(_ => rng.Next())
                .ToList();

            var paired  = new HashSet<int>();
            var pairs   = new List<(int p1, int p2)>();

            for (int i = 0; i < sorted.Count; i++)
            {
                if (paired.Contains(sorted[i].Id)) continue;

                bool found = false;
                for (int j = i + 1; j < sorted.Count; j++)
                {
                    if (paired.Contains(sorted[j].Id)) continue;
                    var key = (Math.Min(sorted[i].Id, sorted[j].Id),
                               Math.Max(sorted[i].Id, sorted[j].Id));
                    if (!played.Contains(key))
                    {
                        pairs.Add((sorted[i].Id, sorted[j].Id));
                        paired.Add(sorted[i].Id);
                        paired.Add(sorted[j].Id);
                        found = true;
                        break;
                    }
                }

                // Se não encontrou adversário válido (todos já se enfrentaram), aceita rematch
                if (!found)
                {
                    for (int j = i + 1; j < sorted.Count; j++)
                    {
                        if (paired.Contains(sorted[j].Id)) continue;
                        pairs.Add((sorted[i].Id, sorted[j].Id));
                        paired.Add(sorted[i].Id);
                        paired.Add(sorted[j].Id);
                        break;
                    }
                }
            }

            // Jogador sem par = BYE
            var byePlayer = sorted.FirstOrDefault(tp => !paired.Contains(tp.Id));

            var matches = pairs.Select(p => new TournamentMatch
            {
                TournamentId = tournamentId,
                MatchType    = 3,
                Round        = round,
                Player1Id    = p.p1,
                Player2Id    = p.p2,
            }).ToList();

            _context.TournamentMatches.AddRange(matches);

            if (byePlayer != null)
            {
                // BYE: vitória automática
                var byeMatch = new TournamentMatch
                {
                    TournamentId = tournamentId,
                    MatchType    = 3,
                    Round        = round,
                    Player1Id    = byePlayer.Id,
                    Player2Id    = null,
                    WinnerId     = byePlayer.Id,
                    IsPlayed     = true,
                    IsBye        = true,
                    Date         = DateTime.UtcNow,
                };
                _context.TournamentMatches.Add(byeMatch);

                byePlayer.SwissPoints += 3;
                byePlayer.SwissWins   += 1;
            }

            await _context.SaveChangesAsync();
        }

        private async Task<List<SwissStandingEntry>> GetStandingsRawAsync(int tournamentId)
        {
            var tps = await _context.TournamentPlayers
                .Where(tp => tp.TournamentId == tournamentId)
                .Include(tp => tp.Player)
                .ToListAsync();

            // Opponent Win Percentage (OMW%) como tiebreaker
            var history = await _context.TournamentMatches
                .Where(m => m.TournamentId == tournamentId && m.MatchType == 3 && m.IsPlayed)
                .ToListAsync();

            var winRateByTp = tps.ToDictionary(
                tp => tp.Id,
                tp => tp.SwissWins + tp.SwissLosses + tp.SwissDraws == 0
                    ? 0.0
                    : (double)tp.SwissWins / (tp.SwissWins + tp.SwissLosses + tp.SwissDraws)
            );

            var omwByTp = tps.ToDictionary(tp => tp.Id, tp =>
            {
                var opponents = history
                    .Where(m => (m.Player1Id == tp.Id && m.Player2Id.HasValue) ||
                                (m.Player2Id == tp.Id && m.Player1Id.HasValue))
                    .Select(m => m.Player1Id == tp.Id ? m.Player2Id!.Value : m.Player1Id!.Value)
                    .Distinct()
                    .ToList();
                if (!opponents.Any()) return 0.0;
                return opponents.Average(o => winRateByTp.TryGetValue(o, out var r) ? r : 0.0);
            });

            return tps
                .OrderByDescending(tp => tp.SwissPoints)
                .ThenByDescending(tp => omwByTp[tp.Id])
                .ThenByDescending(tp => tp.SwissWins)
                .Select((tp, idx) => new SwissStandingEntry
                {
                    Position   = idx + 1,
                    TpId       = tp.Id,
                    PlayerName = tp.DisplayName,
                    Deck       = tp.Deck,
                    Points     = tp.SwissPoints,
                    Wins       = tp.SwissWins,
                    Losses     = tp.SwissLosses,
                    Draws      = tp.SwissDraws,
                    Omw        = Math.Round(omwByTp[tp.Id] * 100, 1),
                })
                .ToList();
        }
    }

    public class SwissStandingEntry
    {
        public int Position   { get; set; }
        public int TpId       { get; set; }
        public string? PlayerName { get; set; }
        public string? Deck    { get; set; }
        public int Points      { get; set; }
        public int Wins        { get; set; }
        public int Losses      { get; set; }
        public int Draws       { get; set; }
        public double Omw      { get; set; }
    }
}
