using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RankingDigi.Models;
using RankingDigi.Data;
using System.Collections;

namespace RankingDigi.Controller
{
    [Route("api/[controller]")]
    [ApiController]

    public class PlayerController : ControllerBase
    {
        private readonly RankingContext _context;

        // Construtor para injetar o RankingContext
        public PlayerController(RankingContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<ActionResult<Player>> AddPlayer(Player player)
        {
            if (player == null || string.IsNullOrWhiteSpace(player.Name))
                return BadRequest(new { error = "Informe o nome do jogador." });

            player.Name = player.Name.Trim();

            bool exists = await _context.Players.AnyAsync(p => p.Name.ToLower() == player.Name.ToLower());
            if (exists)
                return Conflict(new { error = $"Já existe um jogador chamado \"{player.Name}\"." });

            _context.Players.Add(player);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetPlayerById), new { id = player.Id }, player);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePlayer(int id, [FromBody] Player update)
        {
            if (update == null || string.IsNullOrWhiteSpace(update.Name))
                return BadRequest(new { error = "Informe o novo nome do jogador." });

            var player = await _context.Players.FindAsync(id);
            if (player == null) return NotFound(new { error = "Jogador não encontrado." });

            var newName = update.Name.Trim();
            bool exists = await _context.Players.AnyAsync(p => p.Id != id && p.Name.ToLower() == newName.ToLower());
            if (exists)
                return Conflict(new { error = $"Já existe outro jogador chamado \"{newName}\"." });

            player.Name = newName;
            await _context.SaveChangesAsync();
            return Ok(player);
        }

        [HttpGet("{id}/profile")]
        public async Task<IActionResult> GetProfile(int id)
        {
            var player = await _context.Players.FindAsync(id);
            if (player == null) return NotFound(new { error = "Jogador não encontrado." });

            // Posição no ranking (1 = topo)
            var allByScore = await _context.Players
                .OrderByDescending(p => p.Score)
                .ThenBy(p => p.Id)
                .Select(p => p.Id)
                .ToListAsync();
            int position = allByScore.IndexOf(id) + 1;

            // Partidas avulsas do jogador
            var matches = await _context.Matches
                .Where(m => m.Player1Id == id || m.Player2Id == id)
                .OrderBy(m => m.Date)
                .ToListAsync();

            int wins = matches.Count(m => m.WinnerId == id);
            int draws = matches.Count(m => m.WinnerId == 0);
            int losses = matches.Count - wins - draws;
            int played = matches.Count;
            double winRate = played > 0 ? Math.Round((double)wins / played * 100, 1) : 0;

            // Evolução de pontuação (replay)
            int runningScore = 0;
            var scoreHistory = matches.Select(m =>
            {
                if (m.WinnerId == 0) runningScore += 1;
                else if (m.WinnerId == id) runningScore += 3;
                return new
                {
                    Date = m.Date.ToString("yyyy-MM-dd"),
                    Score = runningScore,
                };
            }).ToList();

            // Decks (agregado: usos, vitórias e taxa)
            var deckStats = new Dictionary<string, (int Used, int Wins)>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in matches)
            {
                string? deck = m.Player1Id == id ? m.Deck1 : m.Deck2;
                if (string.IsNullOrWhiteSpace(deck)) continue;
                var key = deck.Trim();
                if (!deckStats.TryGetValue(key, out var stats)) stats = (0, 0);
                stats.Used += 1;
                if (m.WinnerId == id) stats.Wins += 1;
                deckStats[key] = stats;
            }
            var decks = deckStats
                .OrderByDescending(d => d.Value.Used)
                .ThenByDescending(d => d.Value.Wins)
                .Select(d => new
                {
                    Deck = d.Key,
                    Used = d.Value.Used,
                    Wins = d.Value.Wins,
                    WinRate = d.Value.Used > 0 ? Math.Round((double)d.Value.Wins / d.Value.Used * 100, 1) : 0,
                })
                .ToList();

            // Últimas 10 partidas (resumo p/ exibição)
            var recentMatches = matches
                .OrderByDescending(m => m.Date)
                .Take(10)
                .Select(m => new
                {
                    m.Id,
                    m.Date,
                    OpponentId = m.Player1Id == id ? m.Player2Id : m.Player1Id,
                    MyDeck = m.Player1Id == id ? m.Deck1 : m.Deck2,
                    OpponentDeck = m.Player1Id == id ? m.Deck2 : m.Deck1,
                    Result = m.WinnerId == 0 ? "draw" : (m.WinnerId == id ? "win" : "loss"),
                })
                .ToList();

            var opponentIds = recentMatches.Select(r => r.OpponentId).Distinct().ToList();
            var opponentNames = await _context.Players
                .Where(p => opponentIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Name);

            var recentMatchesView = recentMatches.Select(r => new
            {
                r.Id,
                r.Date,
                r.OpponentId,
                OpponentName = opponentNames.TryGetValue(r.OpponentId, out var n) ? n : "Desconhecido",
                r.MyDeck,
                r.OpponentDeck,
                r.Result,
            }).ToList();

            // Torneios em que o jogador participou
            var participations = await _context.TournamentPlayers
                .Where(tp => tp.PlayerId == id)
                .Include(tp => tp.Tournament)
                .ToListAsync();

            var tournaments = new List<object>();
            foreach (var part in participations)
            {
                var t = part.Tournament;
                if (t == null) continue;

                string? finalPosition = null;
                int? winnerOfTournamentId = null;
                int? runnerUpId = null;

                // Determina campeão e vice (grande final)
                var grandFinal = await _context.TournamentMatches
                    .Where(m => m.TournamentId == t.Id && m.MatchType == 2 && m.IsPlayed && m.WinnerId.HasValue)
                    .OrderByDescending(m => m.Round)
                    .FirstOrDefaultAsync();

                if (grandFinal != null)
                {
                    winnerOfTournamentId = grandFinal.WinnerId;
                    runnerUpId = grandFinal.Player1Id == grandFinal.WinnerId ? grandFinal.Player2Id : grandFinal.Player1Id;
                    if (winnerOfTournamentId == id) finalPosition = "1º lugar";
                    else if (runnerUpId == id) finalPosition = "2º lugar";
                }

                tournaments.Add(new
                {
                    t.Id,
                    t.Name,
                    t.StartDate,
                    t.Status,
                    Deck = part.Deck,
                    FinalPosition = finalPosition,
                    IsChampion = winnerOfTournamentId == id,
                });
            }

            return Ok(new
            {
                player = new
                {
                    player.Id,
                    player.Name,
                    player.Score,
                    Position = position,
                    Initials = GetInitials(player.Name ?? ""),
                },
                stats = new
                {
                    played,
                    wins,
                    losses,
                    draws,
                    winRate,
                    championships = tournaments.Count(t => (bool)t.GetType().GetProperty("IsChampion")!.GetValue(t)!),
                },
                scoreHistory,
                decks,
                recentMatches = recentMatchesView,
                tournaments,
            });
        }

        private static string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return parts[0][..Math.Min(2, parts[0].Length)].ToUpper();
            return (parts[0][0].ToString() + parts[^1][0].ToString()).ToUpper();
        }

        [HttpGet("{id}/last-deck")]
        public async Task<IActionResult> GetLastDeck(int id)
        {
            var lastMatch = await _context.Matches
                .Where(m => m.Player1Id == id || m.Player2Id == id)
                .OrderByDescending(m => m.Date)
                .Select(m => new { m.Player1Id, m.Player2Id, m.Deck1, m.Deck2 })
                .FirstOrDefaultAsync();

            if (lastMatch == null) return Ok(new { deck = (string?)null });

            var deck = lastMatch.Player1Id == id ? lastMatch.Deck1 : lastMatch.Deck2;
            return Ok(new { deck });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePlayer(int id)
        {
            var player = await _context.Players.FindAsync(id);
            if (player == null)
            {
                return NotFound();
            }

            // Verificar se o jogador possui partidas
            bool hasMatches = await _context.Matches
                .AnyAsync(m => m.Player1Id == id || m.Player2Id == id);

            if (hasMatches)
            {
                return Conflict("Não é possível excluir um jogador que já participou de partidas.");
            }

            _context.Players.Remove(player);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Player>>> GetPlayer(string? orderBy = "score")
        {
            IQueryable<Player> query = _context.Players;

            if (orderBy?.ToLower() == "name")
            {
                query = query.OrderBy(p => p.Name);
            }
            else
            {
                query = query.OrderByDescending(p => p.Score);
            }

            return await query.ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Player>> GetPlayerById(int id)
        {
            var player = await _context.Players.FindAsync(id);
            if (player == null) return NotFound();
            return player;
        }
    }
}
