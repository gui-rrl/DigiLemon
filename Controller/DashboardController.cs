using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RankingDigi.Data;

namespace RankingDigi.Controller
{
    [ApiController]
    [Route("api/dashboard")]
    public class DashboardController : ControllerBase
    {
        private readonly RankingContext _context;

        public DashboardController(RankingContext context)
        {
            _context = context;
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats([FromQuery] int days = 30)
        {
            if (days < 1) days = 30;
            if (days > 365) days = 365;

            var totalPlayers = await _context.Players.CountAsync();
            var totalMatches = await _context.Matches.CountAsync();
            var totalTournaments = await _context.Tournaments.CountAsync();
            var draws = await _context.Matches.CountAsync(m => m.WinnerId == 0);

            var topPlayers = await _context.Players
                .OrderByDescending(p => p.Score)
                .Take(5)
                .Select(p => new { p.Name, p.Score })
                .ToListAsync();

            // Vitórias por jogador
            var playerWins = await _context.Players
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    Wins = _context.Matches.Count(m => m.WinnerId == p.Id),
                    Played = _context.Matches.Count(m => m.Player1Id == p.Id || m.Player2Id == p.Id),
                })
                .Where(p => p.Played > 0)
                .OrderByDescending(p => p.Wins)
                .Take(8)
                .ToListAsync();

            // Vitórias por deck (combina deck1 e deck2 considerando o vencedor)
            var matches = await _context.Matches
                .Where(m => m.WinnerId != 0 && m.Deck1 != null && m.Deck2 != null)
                .Select(m => new { m.WinnerId, m.Player1Id, m.Player2Id, m.Deck1, m.Deck2 })
                .ToListAsync();

            var deckWins = matches
                .Select(m => m.WinnerId == m.Player1Id ? m.Deck1 : m.Deck2)
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .GroupBy(d => d!.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => new { Deck = g.Key, Wins = g.Count() })
                .OrderByDescending(g => g.Wins)
                .Take(8)
                .ToList();

            // Partidas por dia
            var since = DateTime.Today.AddDays(-(days - 1));
            var byDayRaw = await _context.Matches
                .Where(m => m.Date >= since)
                .Select(m => m.Date.Date)
                .ToListAsync();

            var byDayGrouped = byDayRaw
                .GroupBy(d => d)
                .ToDictionary(g => g.Key, g => g.Count());

            var matchesPerDay = Enumerable.Range(0, days)
                .Select(i => since.AddDays(i))
                .Select(d => new
                {
                    Date = d.ToString("yyyy-MM-dd"),
                    Count = byDayGrouped.TryGetValue(d, out var c) ? c : 0,
                })
                .ToList();

            return Ok(new
            {
                summary = new
                {
                    totalPlayers,
                    totalMatches,
                    totalTournaments,
                    draws,
                    decisive = totalMatches - draws,
                },
                topPlayers,
                playerWins,
                deckWins,
                matchesPerDay,
            });
        }
    }
}
