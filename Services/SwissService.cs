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

            // Todos contra todos (formato 3): todas as partidas saem de uma vez, numa "rodada"
            // única. Sem rodadas para avançar e sem bye — cada um joga contra todos os outros.
            if (tournament.Format == 3)
            {
                tournament.SwissRounds = 1;
                tournament.Status = 1;
                tournament.CurrentSwissRound = 1;
                await _context.SaveChangesAsync();
                await GenerateAllPairingsAsync(tournamentId, tps.Select(tp => tp.Id).ToList());
                return;
            }

            // Garante que rounds foi calculado
            if (tournament.SwissRounds == 0)
                tournament.SwissRounds = CalculateRounds(tps.Count);

            tournament.Status = 1;
            tournament.CurrentSwissRound = 1;
            await _context.SaveChangesAsync();

            await GenerateRoundAsync(tournamentId, 1, tps.Select(tp => tp.Id).ToList());
        }

        /// <summary>
        /// Gera de uma vez as N×(N-1)/2 partidas do todos contra todos. Não há bye: com número
        /// ímpar de jogadores ninguém "folga", já que as partidas não estão presas a rodadas —
        /// cada um joga as suas na ordem que der.
        /// </summary>
        private async Task GenerateAllPairingsAsync(int tournamentId, List<int> tpIds)
        {
            var matches = new List<TournamentMatch>();
            for (int i = 0; i < tpIds.Count; i++)
                for (int j = i + 1; j < tpIds.Count; j++)
                    matches.Add(new TournamentMatch
                    {
                        TournamentId = tournamentId,
                        MatchType    = 3,
                        Round        = 1,
                        Player1Id    = tpIds[i],
                        Player2Id    = tpIds[j],
                        IsPlayed     = false,
                    });

            _context.TournamentMatches.AddRange(matches);
            await _context.SaveChangesAsync();
        }

        // ── Avançar para a próxima rodada ────────────────────────────────────
        public async Task AdvanceRoundAsync(int tournamentId)
        {
            var tournament = await _context.Tournaments.FindAsync(tournamentId)
                ?? throw new InvalidOperationException("Torneio não encontrado.");

            if (tournament.Format == 3)
                throw new InvalidOperationException("Torneio de todos contra todos não tem rodadas para avançar: registre todas as partidas e gere o Top Cut.");

            int currentRound = tournament.CurrentSwissRound;
            if (currentRound == 0)
                throw new InvalidOperationException("Swiss não foi iniciado.");

            var roundMatches = await _context.TournamentMatches
                .Where(m => m.TournamentId == tournamentId && m.MatchType == 3 && m.Round == currentRound)
                .ToListAsync();

            if (roundMatches.Any(m => !m.IsPlayed))
                throw new InvalidOperationException("Todas as partidas da rodada atual precisam ser finalizadas antes de avançar.");

            if (currentRound >= tournament.SwissRounds)
            {
                var msg = tournament.Format == 2
                    ? "Todas as rodadas Swiss já foram concluídas. Encerre o torneio."
                    : "Todas as rodadas Swiss já foram concluídas. Gere o Top Cut.";
                throw new InvalidOperationException(msg);
            }

            tournament.CurrentSwissRound++;
            await _context.SaveChangesAsync();

            var tpIds = await _context.TournamentPlayers
                .Where(tp => tp.TournamentId == tournamentId)
                .Select(tp => tp.Id)
                .ToListAsync();

            await GenerateRoundAsync(tournamentId, tournament.CurrentSwissRound, tpIds);
        }

        // ── Encerrar Swiss Pontos Corridos (sem top cut) ─────────────────────
        public async Task FinishAsync(int tournamentId)
        {
            var tournament = await _context.Tournaments.FindAsync(tournamentId)
                ?? throw new InvalidOperationException("Torneio não encontrado.");

            if (tournament.Format != 2)
                throw new InvalidOperationException("Este endpoint é apenas para Swiss Pontos Corridos.");

            if (tournament.CurrentSwissRound < tournament.SwissRounds)
                throw new InvalidOperationException("O Swiss ainda não terminou.");

            var lastRoundMatches = await _context.TournamentMatches
                .Where(m => m.TournamentId == tournamentId && m.MatchType == 3 && m.Round == tournament.SwissRounds)
                .ToListAsync();

            if (lastRoundMatches.Any(m => !m.IsPlayed))
                throw new InvalidOperationException("Finalize todas as partidas da última rodada antes de encerrar.");

            tournament.Status  = 2;
            tournament.EndDate = DateTime.UtcNow;

            var standings = await GetStandingsRawAsync(tournamentId);
            await TournamentScoringService.AwardSwissStandingsPlacementBonusAsync(_context, tournament, standings);

            await _context.SaveChangesAsync();
        }

        // ── Gerar Top Cut (double elimination com os top N) ──────────────────
        /// <param name="force">
        /// Encerramento antecipado pelo admin: fecha a fase de pontos onde estiver e corta o Top
        /// Cut pela classificação atual. As partidas ainda não jogadas são descartadas — elas não
        /// vão acontecer, e mantê-las deixaria a tela pedindo resultado de jogo que não existe mais.
        /// </param>
        public async Task GenerateTopCutAsync(int tournamentId, bool force = false)
        {
            var tournament = await _context.Tournaments.FindAsync(tournamentId)
                ?? throw new InvalidOperationException("Torneio não encontrado.");

            if (force)
            {
                var pendentes = await _context.TournamentMatches
                    .Where(m => m.TournamentId == tournamentId && m.MatchType == 3 && !m.IsPlayed)
                    .ToListAsync();

                if (!await _context.TournamentMatches.AnyAsync(m =>
                        m.TournamentId == tournamentId && m.MatchType == 3 && m.IsPlayed))
                    throw new InvalidOperationException("Registre ao menos uma partida antes de encerrar a fase de pontos.");

                _context.TournamentMatches.RemoveRange(pendentes);
                tournament.CurrentSwissRound = tournament.SwissRounds; // fase marcada como concluída
                await _context.SaveChangesAsync();
            }
            else
            {
                if (tournament.CurrentSwissRound < tournament.SwissRounds)
                    throw new InvalidOperationException("O Swiss ainda não terminou.");

                var lastRoundMatches = await _context.TournamentMatches
                    .Where(m => m.TournamentId == tournamentId && m.MatchType == 3 && m.Round == tournament.SwissRounds)
                    .ToListAsync();

                if (lastRoundMatches.Any(m => !m.IsPlayed))
                    throw new InvalidOperationException("Finalize todas as partidas da última rodada antes de gerar o Top Cut.");
            }

            int topN = tournament.TopCutSize;
            var standings = await GetStandingsRawAsync(tournamentId);
            var topPlayers = standings.Take(topN).Select(s => s.TpId).ToList();

            if (topPlayers.Count < 2)
                throw new InvalidOperationException("Não há jogadores suficientes para o Top Cut.");

            // Top 4: semifinais com sorteio aleatório + disputa de 3º lugar, sem lower bracket.
            // Top 8 (ou qualquer outro tamanho): dupla eliminação completa, como sempre foi.
            if (topN == 4)
            {
                if (topPlayers.Count < 4)
                    throw new InvalidOperationException("O Top 4 exige pelo menos 4 participantes classificados.");
                var topFour = new TopFourGenerator(_context);
                await topFour.GenerateAsync(tournamentId, topPlayers);
            }
            else
            {
                var generator = new DoubleEliminationGenerator(_context);
                await generator.GenerateAsync(tournamentId, topPlayers);
            }
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

            // Pareia seguindo a mesma ordem da classificação (pontos → OMW% → GW% → OGW% →
            // critério final determinístico). Antes o desempate era aleatório, o que fazia a
            // ordem mudar a cada geração e deixava os desempates sem efeito no chaveamento.
            var playedHistory = history.Where(m => m.IsPlayed).ToList();
            var (_, gwByTp, omwByTp, ogwByTp) = CalculateTiebreakers(tps, playedHistory);

            var sorted = tps
                .OrderByDescending(tp => tp.SwissPoints)
                .ThenByDescending(tp => omwByTp[tp.Id])
                .ThenByDescending(tp => gwByTp[tp.Id])
                .ThenByDescending(tp => ogwByTp[tp.Id])
                .ThenBy(tp => tp.Id)
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

            var history = await _context.TournamentMatches
                .Where(m => m.TournamentId == tournamentId && m.MatchType == 3 && m.IsPlayed)
                .ToListAsync();

            var (mwByTp, gwByTp, omwByTp, ogwByTp) = CalculateTiebreakers(tps, history);

            return tps
                .OrderByDescending(tp => tp.SwissPoints)
                .ThenByDescending(tp => omwByTp[tp.Id])
                .ThenByDescending(tp => gwByTp[tp.Id])
                .ThenByDescending(tp => ogwByTp[tp.Id])
                .ThenBy(tp => tp.Id)   // critério final determinístico: mesma ordem em toda consulta
                .Select((tp, idx) => new SwissStandingEntry
                {
                    Position   = idx + 1,
                    TpId       = tp.Id,
                    PlayerId   = tp.PlayerId,
                    PlayerName = tp.DisplayName,
                    Deck       = tp.Deck,
                    Points     = tp.SwissPoints,
                    Wins       = tp.SwissWins,
                    Losses     = tp.SwissLosses,
                    Draws      = tp.SwissDraws,
                    Omw        = Math.Round(omwByTp[tp.Id] * 100, 1),
                    Gw         = Math.Round(gwByTp[tp.Id] * 100, 1),
                    Ogw        = Math.Round(ogwByTp[tp.Id] * 100, 1),
                })
                .ToList();
        }

        /// <summary>Piso oficial de 33% aplicado ao aproveitamento de cada adversário.</summary>
        private const double MinimumRate = 1.0 / 3.0;

        /// <summary>
        /// Desempates no padrão oficial do Digimon/Magic:
        /// MW% (aproveitamento de partidas), GW% (aproveitamento de games, melhor de 3),
        /// OMW% e OGW% (médias dos adversários, cada um com piso de 33%).
        /// Byes contam como vitória 2x0 para quem recebeu, mas são ignorados nas médias dos
        /// adversários — ninguém "herda" o aproveitamento de um bye.
        /// </summary>
        private static (Dictionary<int, double> Mw, Dictionary<int, double> Gw,
                        Dictionary<int, double> Omw, Dictionary<int, double> Ogw)
            CalculateTiebreakers(List<TournamentPlayer> tps, List<TournamentMatch> history)
        {
            // MW% = pontos / (3 × partidas jogadas)
            var mwByTp = tps.ToDictionary(tp => tp.Id, tp =>
            {
                int played = tp.SwissWins + tp.SwissLosses + tp.SwissDraws;
                return played == 0 ? 0.0 : (double)tp.SwissPoints / (3.0 * played);
            });

            // GW% = games ganhos / games jogados. Partidas sem placar registrado (anteriores ao
            // melhor de 3) e byes entram pela convenção: vitória 2x0, derrota 0x2, empate 1x1.
            var gamesWon = tps.ToDictionary(tp => tp.Id, _ => 0);
            var gamesTotal = tps.ToDictionary(tp => tp.Id, _ => 0);

            foreach (var m in history)
            {
                void Add(int tpId, int won, int lost)
                {
                    if (!gamesWon.ContainsKey(tpId)) return;
                    gamesWon[tpId] += won;
                    gamesTotal[tpId] += won + lost;
                }

                if (m.IsBye)
                {
                    if (m.Player1Id.HasValue) Add(m.Player1Id.Value, 2, 0);
                    continue;
                }
                if (!m.Player1Id.HasValue || !m.Player2Id.HasValue) continue;

                int p1, p2;
                if (m.Player1GameWins.HasValue && m.Player2GameWins.HasValue)
                {
                    p1 = m.Player1GameWins.Value;
                    p2 = m.Player2GameWins.Value;
                }
                else if (m.WinnerId == null) { p1 = 1; p2 = 1; }                  // empate sem placar
                else if (m.WinnerId == m.Player1Id) { p1 = 2; p2 = 0; }
                else { p1 = 0; p2 = 2; }

                Add(m.Player1Id.Value, p1, p2);
                Add(m.Player2Id.Value, p2, p1);
            }

            var gwByTp = tps.ToDictionary(tp => tp.Id, tp =>
                gamesTotal[tp.Id] == 0 ? 0.0 : (double)gamesWon[tp.Id] / gamesTotal[tp.Id]);

            // Adversários enfrentados de verdade (bye não tem adversário)
            var opponentsByTp = tps.ToDictionary(tp => tp.Id, tp => history
                .Where(m => !m.IsBye &&
                            ((m.Player1Id == tp.Id && m.Player2Id.HasValue) ||
                             (m.Player2Id == tp.Id && m.Player1Id.HasValue)))
                .Select(m => m.Player1Id == tp.Id ? m.Player2Id!.Value : m.Player1Id!.Value)
                .Distinct()
                .ToList());

            double AverageOfOpponents(List<int> opponents, Dictionary<int, double> rates) =>
                opponents.Count == 0
                    ? 0.0
                    : opponents.Average(o => Math.Max(MinimumRate, rates.TryGetValue(o, out var r) ? r : 0.0));

            var omwByTp = tps.ToDictionary(tp => tp.Id, tp => AverageOfOpponents(opponentsByTp[tp.Id], mwByTp));
            var ogwByTp = tps.ToDictionary(tp => tp.Id, tp => AverageOfOpponents(opponentsByTp[tp.Id], gwByTp));

            return (mwByTp, gwByTp, omwByTp, ogwByTp);
        }
    }

    public class SwissStandingEntry
    {
        public int Position   { get; set; }
        public int TpId       { get; set; }
        public int? PlayerId  { get; set; }
        public string? PlayerName { get; set; }
        public string? Deck    { get; set; }
        public int Points      { get; set; }
        public int Wins        { get; set; }
        public int Losses      { get; set; }
        public int Draws       { get; set; }
        public double Omw      { get; set; }   // aproveitamento médio dos adversários (partidas)
        public double Gw       { get; set; }   // aproveitamento próprio em games (melhor de 3)
        public double Ogw      { get; set; }   // aproveitamento médio dos adversários em games
    }
}
