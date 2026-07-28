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
        private readonly MatchReportCodeService _reportCodes;

        public TournamentMatchController(RankingContext context, MatchResultService matchResults, MatchReportCodeService reportCodes)
        {
            _context = context;
            _matchResults = matchResults;
            _reportCodes = reportCodes;
        }

        // A regra de negócio vive em MatchResultService para ser compartilhada com a
        // integração do DCGO (api/integration), que aplica resultados pelo mesmo caminho.
        [HttpPost("{id}/result")]
        public async Task<IActionResult> SetMatchResult(int id, [FromBody] MatchResultDto result)
        {
            var match = await _context.TournamentMatches.FindAsync(id);
            if (match == null) return NotFound();

            var outcome = await _matchResults.ApplyAsync(match, result);
            if (!outcome.Success) return BadRequest(new { error = outcome.Error });

            return Ok();
        }
        // GET: api/tournamentmatch/{id}/reports — os relatos que o DCGO enviou para esta
        // partida, com nomes resolvidos, para o admin julgar um conflito sem consultar o banco.
        [HttpGet("{id}/reports")]
        public async Task<IActionResult> GetMatchReports(int id)
        {
            var match = await _context.TournamentMatches.FindAsync(id);
            if (match == null) return NotFound();

            var relatos = await _context.MatchReports
                .Where(r => r.TournamentMatchId == id)
                .OrderBy(r => r.PlayerSlot)
                .ToListAsync();

            var tpIds = new[] { match.Player1Id, match.Player2Id }
                .Where(i => i.HasValue).Select(i => i!.Value).ToList();
            var jogadores = await _context.TournamentPlayers
                .Include(tp => tp.Player)
                .Where(tp => tpIds.Contains(tp.Id))
                .ToDictionaryAsync(tp => tp.Id, tp => tp.DisplayName);

            string Nome(int? tpId) => tpId.HasValue && jogadores.TryGetValue(tpId.Value, out var n) ? n : "—";

            return Ok(relatos.Select(r => new
            {
                r.PlayerSlot,
                playerName = Nome(r.PlayerSlot == 1 ? match.Player1Id : match.Player2Id),
                claimedWinnerName = r.ClaimedWinnerTpId == null ? "Empate" : Nome(r.ClaimedWinnerTpId),
                claimedScore = $"{r.ClaimedPlayer1GameWins}-{r.ClaimedPlayer2GameWins}",
                r.ReporterNickname,
                r.ClientVersion,
                r.RevisionCount,
                reportedAt = r.UpdatedAt ?? r.CreatedAt,
            }).ToList());
        }

        // POST: api/tournamentmatch/{id}/regenerate-report-codes — troca os dois códigos e
        // descarta os relatos (vazamento ou jogador que perdeu o código). Espelha o
        // regenerate-invite do TournamentController.
        [HttpPost("{id}/regenerate-report-codes")]
        public async Task<IActionResult> RegenerateReportCodes(int id)
        {
            var match = await _context.TournamentMatches.FindAsync(id);
            if (match == null) return NotFound();

            var tournament = await _context.Tournaments.FindAsync(match.TournamentId);
            if (tournament?.Mode != 1)
                return BadRequest(new { error = "Códigos de relato existem apenas em torneios online." });

            var (p1, p2) = await _reportCodes.RegenerateAsync(match);
            return Ok(new
            {
                matchId = match.Id,
                player1Code = p1,
                player2Code = p2,
            });
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
