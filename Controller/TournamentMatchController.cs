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

            bool isDraw = result.WinnerId == 0;
            if (isDraw && match.MatchType != 3)
                return BadRequest(new { error = "Empate não é permitido em fases eliminatórias — a partida precisa de um vencedor (morte súbita)." });
            if (!isDraw && result.WinnerId != match.Player1Id && result.WinnerId != match.Player2Id)
                return BadRequest(new { error = "O vencedor precisa ser um dos jogadores da partida (ou 0 para empate, apenas no Swiss)." });

            match.WinnerId = isDraw ? null : result.WinnerId;
            match.IsPlayed = true;
            match.Date = DateTime.UtcNow;

            var tournament = await _context.Tournaments.FindAsync(match.TournamentId);

            // ── Swiss: atualizar pontos/vitórias/derrotas/empates dos jogadores ─
            if (match.MatchType == 3 && !match.IsBye)
            {
                if (isDraw)
                {
                    var tp1 = match.Player1Id.HasValue ? await _context.TournamentPlayers.FindAsync(match.Player1Id.Value) : null;
                    var tp2 = match.Player2Id.HasValue ? await _context.TournamentPlayers.FindAsync(match.Player2Id.Value) : null;
                    if (tp1 != null) { tp1.SwissPoints += 1; tp1.SwissDraws += 1; }
                    if (tp2 != null) { tp2.SwissPoints += 1; tp2.SwissDraws += 1; }
                }
                else
                {
                    int? loserId = match.Player1Id == (int?)result.WinnerId ? match.Player2Id : match.Player1Id;

                    var winner = await _context.TournamentPlayers.FindAsync(result.WinnerId);
                    if (winner != null) { winner.SwissPoints += 3; winner.SwissWins += 1; }

                    if (loserId.HasValue)
                    {
                        var loser = await _context.TournamentPlayers.FindAsync(loserId.Value);
                        if (loser != null) loser.SwissLosses += 1;
                    }
                }

                if (tournament != null)
                    await TournamentScoringService.AwardMatchResultAsync(_context, match, tournament);

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

            if (tournament != null)
                await TournamentScoringService.AwardMatchResultAsync(_context, match, tournament);

            // Quando a Grande Final é concluída, marca o torneio como Finalizado (Status = 2)
            // e premia campeão/vice/3º lugar no ranking geral.
            if (match.MatchType == 2 && tournament != null)
            {
                tournament.Status = 2;
                await TournamentScoringService.AwardBracketPlacementBonusAsync(_context, tournament);
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
