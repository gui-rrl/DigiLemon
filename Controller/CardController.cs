using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RankingDigi.Data;
using RankingDigi.Services;

namespace RankingDigi.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class CardController : ControllerBase
    {
        private readonly RankingContext _context;
        private readonly CardSyncService _syncService;

        public CardController(RankingContext context, CardSyncService syncService)
        {
            _context = context;
            _syncService = syncService;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetCards(
            [FromQuery] string? name = null,
            [FromQuery] string? color = null,
            [FromQuery] string? type = null,
            [FromQuery] string? set = null,
            [FromQuery] int? cost = null,
            [FromQuery] int? level = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 60)
        {
            var query = _context.Cards.AsQueryable();

            if (!string.IsNullOrWhiteSpace(name))
                query = query.Where(c => c.Name.Contains(name) || c.CardNumber.Contains(name));
            if (!string.IsNullOrWhiteSpace(color))
                query = query.Where(c => c.Color == color || c.Color2 == color);
            if (!string.IsNullOrWhiteSpace(type))
                query = query.Where(c => c.Type == type);
            if (!string.IsNullOrWhiteSpace(set))
                query = query.Where(c => c.CardNumber.StartsWith(set + "-"));
            if (cost.HasValue)
                query = query.Where(c => c.PlayCost == cost.Value);
            if (level.HasValue)
                query = query.Where(c => c.Level == level.Value);

            pageSize = Math.Clamp(pageSize, 1, 200);
            page = Math.Max(page, 1);

            var total = await query.CountAsync();
            var items = await query
                .OrderBy(c => c.CardNumber)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new { total, page, pageSize, items });
        }

        // Opções disponíveis pros filtros de coleção/custo/nível — computadas a partir dos dados
        // atuais em vez de fixas, já que a lista de sets do jogo cresce a cada sync.
        [HttpGet("filter-options")]
        [AllowAnonymous]
        public async Task<IActionResult> GetFilterOptions()
        {
            var cardNumbers = await _context.Cards.Select(c => c.CardNumber).ToListAsync();
            var sets = cardNumbers
                .Select(cn => cn.Split('-')[0])
                .Distinct()
                .Select(code =>
                {
                    var match = System.Text.RegularExpressions.Regex.Match(code, @"^([A-Za-z]*)(\d*)$");
                    var number = match.Groups[2].Success && match.Groups[2].Value.Length > 0
                        ? int.Parse(match.Groups[2].Value)
                        : 0;
                    return (Code: code, Letters: match.Groups[1].Value, Number: number);
                })
                .OrderBy(x => x.Letters)
                .ThenBy(x => x.Number)
                .Select(x => x.Code)
                .ToList();

            var costs = await _context.Cards
                .Where(c => c.PlayCost != null)
                .Select(c => c.PlayCost!.Value)
                .Distinct()
                .OrderBy(v => v)
                .ToListAsync();

            var levels = await _context.Cards
                .Where(c => c.Level != null)
                .Select(c => c.Level!.Value)
                .Distinct()
                .OrderBy(v => v)
                .ToListAsync();

            return Ok(new { sets, costs, levels });
        }

        [HttpGet("restrictions")]
        [AllowAnonymous]
        public async Task<IActionResult> GetRestrictions()
        {
            var restrictions = await _context.CardRestrictions.ToListAsync();
            var pairs = await _context.BannedPairs.ToListAsync();
            return Ok(new { restrictions, pairs });
        }

        [HttpPost("sync")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SyncCards()
        {
            var count = await _syncService.SyncCardsAsync();
            return Ok(new { message = $"{count} cartas sincronizadas.", count });
        }
    }
}
