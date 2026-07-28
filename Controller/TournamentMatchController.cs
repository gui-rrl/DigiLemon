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
        private readonly MatchResultService _matchResults;

        public TournamentMatchController(RankingContext context, MatchResultService matchResults)
        {
            _context = context;
            _matchResults = matchResults;
        }

        // A regra de negócio vive em MatchResultService para poder ser reaproveitada por
        // outros caminhos que apliquem resultado, sem duplicar a lógica de pontuação.
        [HttpPost("{id}/result")]
        public async Task<IActionResult> SetMatchResult(int id, [FromBody] MatchResultDto result)
        {
            var match = await _context.TournamentMatches.FindAsync(id);
            if (match == null) return NotFound();

            var outcome = await _matchResults.ApplyAsync(match, result);
            if (!outcome.Success) return BadRequest(new { error = outcome.Error });

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
