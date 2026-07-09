using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RankingDigi.Data;
using RankingDigi.Models;

namespace RankingDigi.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class MatchesController : ControllerBase
    {
        private readonly RankingContext _context;

        public MatchesController(RankingContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<ActionResult<Match>> AddMatch(Match match)
        {
            if (match == null)
                return BadRequest(new { error = "Dados da partida inválidos." });

            if (match.Player1Id == match.Player2Id)
                return BadRequest(new { error = "Os jogadores precisam ser diferentes." });

            var player1 = await _context.Players.FindAsync(match.Player1Id);
            var player2 = await _context.Players.FindAsync(match.Player2Id);

            if (player1 == null || player2 == null)
                return BadRequest(new { error = "Um dos jogadores não foi encontrado." });

            // Se um deck salvo foi informado, valida o dono e usa o nome dele como texto de exibição
            if (match.Deck1Id.HasValue)
            {
                var deck1 = await _context.Decks.FindAsync(match.Deck1Id.Value);
                if (deck1 == null) return BadRequest(new { error = "Deck do jogador 1 não encontrado." });
                if (deck1.PlayerId != match.Player1Id) return BadRequest(new { error = "O deck selecionado não pertence ao jogador 1." });
                match.Deck1 = deck1.Name;
            }
            if (match.Deck2Id.HasValue)
            {
                var deck2 = await _context.Decks.FindAsync(match.Deck2Id.Value);
                if (deck2 == null) return BadRequest(new { error = "Deck do jogador 2 não encontrado." });
                if (deck2.PlayerId != match.Player2Id) return BadRequest(new { error = "O deck selecionado não pertence ao jogador 2." });
                match.Deck2 = deck2.Name;
            }

            if (string.IsNullOrWhiteSpace(match.Deck1) || string.IsNullOrWhiteSpace(match.Deck2))
                return BadRequest(new { error = "Informe os decks de ambos os jogadores." });

            // Empate ou vitória precisa ser id válido (0 = empate, ou um dos dois jogadores)
            if (match.WinnerId != 0 && match.WinnerId != player1.Id && match.WinnerId != player2.Id)
                return BadRequest(new { error = "O vencedor precisa ser um dos jogadores da partida (ou 0 para empate)." });

            if (match.WinnerId == 0)
            {
                player1.Score += 1;
                player1.CareerScore += 1;
                player2.Score += 1;
                player2.CareerScore += 1;
            }
            else if (match.WinnerId == player1.Id)
            {
                player1.Score += 3;
                player1.CareerScore += 3;
            }
            else
            {
                player2.Score += 3;
                player2.CareerScore += 3;
            }

            if (match.Date == default) match.Date = DateTime.UtcNow;

            var currentSeason = await _context.Seasons.FirstOrDefaultAsync(s => s.IsActive);
            match.SeasonId = currentSeason?.Id;

            _context.Matches.Add(match);
            await _context.SaveChangesAsync();
            return Ok(match);
        }
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Match>>> GetMatches(
                [FromQuery] int playerId = 0,
                [FromQuery] DateTime startDate = default,
                [FromQuery] DateTime endDate = default,
                [FromQuery] string deck = "",
                [FromQuery] int seasonId = 0)
        {
            // adapte a lógica para verificar se o parâmetro foi fornecido
            var query = _context.Matches.AsQueryable();

            if (playerId != 0)
                query = query.Where(m => m.Player1Id == playerId || m.Player2Id == playerId);

            if (seasonId != 0)
                query = query.Where(m => m.SeasonId == seasonId);

            if (startDate != default)
                query = query.Where(m => m.Date >= startDate);

            if (endDate != default)
            {
                var end = endDate.Date.AddDays(1);
                query = query.Where(m => m.Date < end);
            }

            if (!string.IsNullOrEmpty(deck))
                query = query.Where(m => m.Deck1.Contains(deck) || m.Deck2.Contains(deck));

            return await query.OrderByDescending(m => m.Date).ToListAsync();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteMatch(int id)
        {
            var match = await _context.Matches.FindAsync(id);
            if (match == null)
                return NotFound();

            if (match.SeasonId.HasValue)
            {
                var matchSeason = await _context.Seasons.FindAsync(match.SeasonId.Value);
                if (matchSeason != null && !matchSeason.IsActive)
                    return Conflict(new { error = "Não é possível excluir uma partida de uma temporada já encerrada." });
            }

            var player1 = await _context.Players.FindAsync(match.Player1Id);
            var player2 = await _context.Players.FindAsync(match.Player2Id);

            // Reverte os pontos que a partida havia concedido (temporada atual e geral)
            if (match.WinnerId == 0)
            {
                if (player1 != null) { player1.Score -= 1; player1.CareerScore -= 1; }
                if (player2 != null) { player2.Score -= 1; player2.CareerScore -= 1; }
            }
            else if (match.WinnerId == match.Player1Id)
            {
                if (player1 != null) { player1.Score -= 3; player1.CareerScore -= 3; }
            }
            else if (match.WinnerId == match.Player2Id)
            {
                if (player2 != null) { player2.Score -= 3; player2.CareerScore -= 3; }
            }

            _context.Matches.Remove(match);
            await _context.SaveChangesAsync();
            return NoContent();
        }

    }
}
