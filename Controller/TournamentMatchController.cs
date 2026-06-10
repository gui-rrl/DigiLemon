using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RankingDigi.Data;
using RankingDigi.Models;
using Microsoft.EntityFrameworkCore;
using RankingDigi.Services;
using System.Diagnostics.Eventing.Reader;

namespace RankingDigi.Controller
{
    [ApiController]
    [Route("api/tournamentmatch")]
    [Authorize(Roles = "Admin")]
    public class TournamentMatchController : ControllerBase
    {
        private readonly RankingContext _context;

        public TournamentMatchController(RankingContext context)
        {
            _context = context;
        }
        [HttpPost("{id}/result")]
        public async Task<IActionResult> SetMatchResult(int id, [FromBody] MatchResultDto result)
        {
            var match = await _context.TournamentMatches.FindAsync(id);
            if (match == null) return NotFound();

            if (match.IsPlayed)
                return BadRequest(new { error = "Esta partida já foi finalizada." });

            match.WinnerId = result.WinnerId;
            match.IsPlayed = true;
            match.Date = DateTime.UtcNow;

            // ── Swiss: atualizar pontos dos jogadores ──────────────────────────
            if (match.MatchType == 3 && !match.IsBye)
            {
                int? loserId = match.Player1Id == (int?)result.WinnerId ? match.Player2Id : match.Player1Id;

                var winner = await _context.TournamentPlayers.FindAsync(result.WinnerId);
                if (winner != null) { winner.SwissPoints += 3; winner.SwissWins += 1; }

                if (loserId.HasValue)
                {
                    var loser = await _context.TournamentPlayers.FindAsync(loserId.Value);
                    if (loser != null) loser.SwissLosses += 1;
                }

                await _context.SaveChangesAsync();
                return Ok();
            }

            // ── Double Elimination: avança vencedor/perdedor ───────────────────
            if (match.NextMatchId.HasValue)
            {
                var nextMatch = await _context.TournamentMatches.FindAsync(match.NextMatchId.Value);
                if (nextMatch != null)
                {
                    if (match.NextMatchPosition == 1)
                        nextMatch.Player1Id = result.WinnerId;
                    else
                        nextMatch.Player2Id = result.WinnerId;
                }
            }

            if (match.LoserGoesToMatchId.HasValue && result.LoserId.HasValue)
            {
                var loserMatch = await _context.TournamentMatches.FindAsync(match.LoserGoesToMatchId.Value);
                if (loserMatch != null)
                {
                    if (loserMatch.Player2Id == null)
                        loserMatch.Player2Id = result.LoserId;
                    else if (loserMatch.Player1Id == null)
                        loserMatch.Player1Id = result.LoserId;
                    else
                        Console.WriteLine($"ERRO: partida lower {loserMatch.Id} já está cheia!");
                }
            }

            await _context.SaveChangesAsync();
            return Ok();
        }
        [HttpGet("{id}")]
        public async Task<ActionResult<TournamentMatch>> GetTournamentMatch(int id)
        {
            var matchDto = await _context.TournamentMatches
               .Where(m => m.Id == id)
               .Select(m => new TournamentMatchDto
               {
                   Id = m.Id,
                   Player1Id = m.Player1Id,
                   Player2Id = m.Player2Id,
                   WinnerId = m.WinnerId,
                   Date = m.Date,
                   IsPlayed = m.IsPlayed
               })
               .FirstOrDefaultAsync();

            if (matchDto == null)
                return NotFound();

            return Ok(matchDto);
        }
    }
}
