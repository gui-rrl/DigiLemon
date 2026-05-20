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

            if (string.IsNullOrWhiteSpace(match.Deck1) || string.IsNullOrWhiteSpace(match.Deck2))
                return BadRequest(new { error = "Informe os decks de ambos os jogadores." });

            var player1 = await _context.Players.FindAsync(match.Player1Id);
            var player2 = await _context.Players.FindAsync(match.Player2Id);

            if (player1 == null || player2 == null)
                return BadRequest(new { error = "Um dos jogadores não foi encontrado." });

            // Empate ou vitória precisa ser id válido (0 = empate, ou um dos dois jogadores)
            if (match.WinnerId != 0 && match.WinnerId != player1.Id && match.WinnerId != player2.Id)
                return BadRequest(new { error = "O vencedor precisa ser um dos jogadores da partida (ou 0 para empate)." });

            if (match.WinnerId == 0)
            {
                player1.Score += 1;
                player2.Score += 1;
            }
            else if (match.WinnerId == player1.Id)
            {
                player1.Score += 3;
            }
            else
            {
                player2.Score += 3;
            }

            if (match.Date == default) match.Date = DateTime.UtcNow;

            _context.Matches.Add(match);
            await _context.SaveChangesAsync();
            return Ok(match);
        }
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Match>>> GetMatches(
                [FromQuery] int playerId = 0,
                [FromQuery] DateTime startDate = default,
                [FromQuery] DateTime endDate = default,
                [FromQuery] string deck = "")
        {
            // adapte a lógica para verificar se o parâmetro foi fornecido
            var query = _context.Matches.AsQueryable();

            if (playerId != 0)
                query = query.Where(m => m.Player1Id == playerId || m.Player2Id == playerId);

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

    }
}
