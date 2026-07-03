using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RankingDigi.Data;
using RankingDigi.Models;
using RankingDigi.Services;

namespace RankingDigi.Controller
{
    public class StartSeasonDto
    {
        public string Name { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class SeasonController : ControllerBase
    {
        private readonly RankingContext _context;

        public SeasonController(RankingContext context)
        {
            _context = context;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<Season>>> GetSeasons()
        {
            return await _context.Seasons.OrderByDescending(s => s.StartDate).ToListAsync();
        }

        [HttpGet("current")]
        [AllowAnonymous]
        public async Task<ActionResult<Season>> GetCurrentSeason()
        {
            var season = await _context.Seasons.FirstOrDefaultAsync(s => s.IsActive);
            if (season == null) return NoContent();
            return Ok(season);
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<Season>> GetSeasonById(int id)
        {
            var season = await _context.Seasons.FindAsync(id);
            if (season == null) return NotFound();
            return Ok(season);
        }

        [HttpGet("{id}/standings")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<object>>> GetStandings(int id)
        {
            var season = await _context.Seasons.FindAsync(id);
            if (season == null) return NotFound();

            if (season.IsActive)
            {
                var live = await _context.Players
                    .OrderByDescending(p => p.Score)
                    .Select(p => new { p.Id, p.Name, p.AvatarUrl, Score = p.Score })
                    .ToListAsync();
                return Ok(live);
            }

            var snapshot = await _context.PlayerSeasonScores
                .Where(s => s.SeasonId == id)
                .Join(_context.Players, s => s.PlayerId, p => p.Id, (s, p) => new { p.Id, p.Name, p.AvatarUrl, Score = s.FinalScore })
                .OrderByDescending(s => s.Score)
                .ToListAsync();
            return Ok(snapshot);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<Season>> StartSeason(StartSeasonDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.Name))
                return BadRequest(new { error = "Informe o nome da temporada." });

            if (dto.StartDate == default || dto.EndDate == default)
                return BadRequest(new { error = "Informe as datas de início e término da temporada." });

            if (dto.EndDate.Date <= dto.StartDate.Date)
                return BadRequest(new { error = "A data de término precisa ser depois da data de início." });

            bool hasActive = await _context.Seasons.AnyAsync(s => s.IsActive);
            if (hasActive)
                return Conflict(new { error = "Já existe uma temporada em andamento. Encerre-a antes de iniciar uma nova." });

            var season = new Season
            {
                Name = dto.Name.Trim(),
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                IsActive = true,
            };
            _context.Seasons.Add(season);
            await _context.SaveChangesAsync();

            // Vincula retroativamente partidas antigas que ainda não pertenciam a nenhuma temporada
            await _context.Matches
                .Where(m => m.SeasonId == null)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.SeasonId, season.Id));

            return Ok(season);
        }

        // Encerramento manual/antecipado (Admin). O encerramento automático na data de término
        // é feito pelo SeasonAutoEndService.
        [HttpPost("{id}/end")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EndSeason(int id)
        {
            var season = await _context.Seasons.FindAsync(id);
            if (season == null) return NotFound();
            if (!season.IsActive)
                return Conflict(new { error = "Esta temporada já foi encerrada." });

            using var transaction = await _context.Database.BeginTransactionAsync();
            await SeasonService.CloseSeasonAsync(_context, season);
            await transaction.CommitAsync();

            return Ok(season);
        }
    }
}
