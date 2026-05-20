using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RankingDigi.Data;
using RankingDigi.Data;
using RankingDigi.DTOs;
using RankingDigi.Models;
using RankingDigi.Services;
using System.Linq;


[ApiController]
[Route("api/[controller]")]
public class TournamentController : ControllerBase
{
    private readonly RankingContext _context;
    private readonly TournamentService _tournamentService; 


    public TournamentController(RankingContext context, TournamentService tournamentService)
    {
        _context = context;
        _tournamentService = tournamentService;
    }

   //GET: api/tournament
   [HttpGet]
    public async Task<ActionResult<IEnumerable<TournamentDto>>> GetTournaments()
    {
        var tournaments = await _context.Tournaments
            .Include(t => t.Brackets)
                .ThenInclude(b => b.Matches)
            .ToListAsync();

        // Backfill: gera InviteCode para torneios antigos
        bool changed = false;
        foreach (var t in tournaments.Where(x => string.IsNullOrEmpty(x.InviteCode)))
        {
            t.InviteCode = await GenerateUniqueInviteCodeAsync();
            changed = true;
        }
        if (changed) await _context.SaveChangesAsync();

        var dtos = tournaments.Select(t => new TournamentDto
        {
            Id = t.Id,
            Name = t.Name,
            StartDate = t.StartDate,
            Status = t.Status,
            InviteCode = t.InviteCode,
            Brackets = t.Brackets?.Select(b => new BracketDto
            {
                Id = b.Id,
                TournamentId = b.TournamentId,
                Name = b.Name,
                Round = b.Round,
                Order = b.Order,
                Matches = b.Matches?.Select(m => new TournamentMatchDto
                {
                    Id = m.Id,
                    Player1Id = m.Player1Id,
                    Player2Id = m.Player2Id,
                    WinnerId = m.WinnerId,
                    NextMatchId = m.NextMatchId,
                    NextMatchPosition = m.NextMatchPosition,
                    Date = m.Date,
                    IsPlayed = m.IsPlayed
                }).ToList() ?? new List<TournamentMatchDto>()
            }).ToList() ?? new List<BracketDto>()
        }).ToList();

        return Ok(dtos);
    }

    //POST: api/tournament
    [HttpPost]
    public async Task<IActionResult> CreateTournament([FromBody] CreateTournamentDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { error = "Informe o nome do torneio." });

        var players = dto.Players ?? new List<PlayerDeckDto>();

        // Permite criar torneio vazio (sem participantes) para depois convidar via link.
        // Se houver participantes informados na criação, valida normalmente.
        if (players.Count > 0)
        {
            var duplicatedIds = players
                .GroupBy(p => p.PlayerId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicatedIds.Any())
            {
                var duplicatedNames = await _context.Players
                    .Where(p => duplicatedIds.Contains(p.Id))
                    .Select(p => p.Name)
                    .ToListAsync();
                return BadRequest(new
                {
                    error = $"Cada jogador só pode participar uma vez no torneio. Duplicados: {string.Join(", ", duplicatedNames)}."
                });
            }

            var providedIds = players.Select(p => p.PlayerId).Distinct().ToList();
            var existingIds = await _context.Players
                .Where(p => providedIds.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync();
            var missingIds = providedIds.Except(existingIds).ToList();
            if (missingIds.Any())
                return BadRequest(new { error = $"Jogador(es) não encontrado(s): {string.Join(", ", missingIds)}." });
        }

        var tournament = new Tournament
        {
            Name = dto.Name,
            StartDate = dto.StartDate,
            Status = 0,
            InviteCode = await GenerateUniqueInviteCodeAsync(),
        };
        _context.Tournaments.Add(tournament);
        await _context.SaveChangesAsync();

        if (players.Count > 0)
        {
            var tournamentPlayers = players.Select(p => new TournamentPlayer
            {
                TournamentId = tournament.Id,
                PlayerId = p.PlayerId,
                Deck = p.Deck
            }).ToList();
            _context.TournamentPlayers.AddRange(tournamentPlayers);
            await _context.SaveChangesAsync();
        }

        return CreatedAtAction(nameof(GetTournament), new { id = tournament.Id }, new { tournament.Id, tournament.Name, tournament.InviteCode });
    }

    private async Task<string> GenerateUniqueInviteCodeAsync()
    {
        // Código curto, fácil de compartilhar, sem caracteres ambíguos (0/O, 1/I/L).
        const string alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        var rng = Random.Shared;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            var code = new string(Enumerable.Range(0, 8).Select(_ => alphabet[rng.Next(alphabet.Length)]).ToArray());
            bool exists = await _context.Tournaments.AnyAsync(t => t.InviteCode == code);
            if (!exists) return code;
        }
        // Fallback praticamente impossível de chegar
        return Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();
    }

    //GET: api/tournament/invite/{code} - rota pública (sem API key)
    [HttpGet("invite/{code}")]
    public async Task<IActionResult> GetByInviteCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return NotFound(new { error = "Código inválido." });

        var tournament = await _context.Tournaments
            .FirstOrDefaultAsync(t => t.InviteCode == code);
        if (tournament == null)
            return NotFound(new { error = "Torneio não encontrado. Verifique o link." });

        var participants = await _context.TournamentPlayers
            .Where(tp => tp.TournamentId == tournament.Id)
            .Include(tp => tp.Player)
            .Select(tp => new { tp.PlayerId, PlayerName = tp.Player!.Name, tp.Deck })
            .ToListAsync();

        return Ok(new
        {
            tournament.Id,
            tournament.Name,
            tournament.StartDate,
            tournament.Status,
            tournament.InviteCode,
            Participants = participants,
            IsOpenForJoin = tournament.Status == 0,
        });
    }

    //POST: api/tournament/invite/{code}/join - rota pública (sem API key)
    [HttpPost("invite/{code}/join")]
    public async Task<IActionResult> JoinByInvite(string code, [FromBody] JoinTournamentDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Deck))
            return BadRequest(new { error = "Informe o nome e o deck para ingressar no torneio." });

        var tournament = await _context.Tournaments.FirstOrDefaultAsync(t => t.InviteCode == code);
        if (tournament == null)
            return NotFound(new { error = "Torneio não encontrado." });
        if (tournament.Status != 0)
            return BadRequest(new { error = "Este torneio já foi iniciado. Novos jogadores não podem mais ingressar." });

        var name = dto.Name.Trim();
        var deck = dto.Deck.Trim();

        // Reaproveita jogador existente pelo nome (case-insensitive) ou cria novo.
        var player = await _context.Players.FirstOrDefaultAsync(p => p.Name.ToLower() == name.ToLower());
        if (player == null)
        {
            player = new Player { Name = name, Score = 0 };
            _context.Players.Add(player);
            await _context.SaveChangesAsync();
        }

        bool alreadyIn = await _context.TournamentPlayers
            .AnyAsync(tp => tp.TournamentId == tournament.Id && tp.PlayerId == player.Id);
        if (alreadyIn)
            return Conflict(new { error = $"\"{player.Name}\" já está inscrito neste torneio." });

        _context.TournamentPlayers.Add(new TournamentPlayer
        {
            TournamentId = tournament.Id,
            PlayerId = player.Id,
            Deck = deck,
        });
        await _context.SaveChangesAsync();

        return Ok(new
        {
            tournamentName = tournament.Name,
            playerName = player.Name,
            message = $"Você foi inscrito como \"{player.Name}\" com o deck \"{deck}\".",
        });
    }

    //DELETE: api/tournament/{id}/participants/{playerId} - remove participante (somente em preparação)
    [HttpDelete("{id}/participants/{playerId}")]
    public async Task<IActionResult> RemoveParticipant(int id, int playerId)
    {
        var tournament = await _context.Tournaments.FindAsync(id);
        if (tournament == null) return NotFound(new { error = "Torneio não encontrado." });
        if (tournament.Status != 0)
            return BadRequest(new { error = "Não é possível remover participantes após o torneio ser iniciado." });

        var participation = await _context.TournamentPlayers
            .FirstOrDefaultAsync(tp => tp.TournamentId == id && tp.PlayerId == playerId);
        if (participation == null) return NotFound(new { error = "Participante não encontrado." });

        _context.TournamentPlayers.Remove(participation);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    //POST: api/tournament/{id}/regenerate-invite - novo código de convite
    [HttpPost("{id}/regenerate-invite")]
    public async Task<IActionResult> RegenerateInvite(int id)
    {
        var tournament = await _context.Tournaments.FindAsync(id);
        if (tournament == null) return NotFound();
        tournament.InviteCode = await GenerateUniqueInviteCodeAsync();
        await _context.SaveChangesAsync();
        return Ok(new { tournament.InviteCode });
    }

    //GET: api/tournament/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<TournamentDto>> GetTournament(int id)
    {
        var tournament = await _context.Tournaments
            .Include(t => t.Brackets)
                .ThenInclude(b => b.Matches)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tournament == null) return NotFound();

        // Carrega todos os participantes do torneio com seus decks
        var tournamentPlayers = await _context.TournamentPlayers
            .Where(tp => tp.TournamentId == id)
            .ToDictionaryAsync(tp => tp.PlayerId, tp => tp.Deck);

        var dto = new TournamentDto
        {
            Id = tournament.Id,
            Name = tournament.Name,
            StartDate = tournament.StartDate,
            Status = tournament.Status,
            InviteCode = tournament.InviteCode,
            Brackets = tournament.Brackets?.Select(b => new BracketDto
            {
                Id = b.Id,
                TournamentId = b.TournamentId,
                Name = b.Name,
                Round = b.Round,
                Order = b.Order,
                Matches = b.Matches?.Select(m => new TournamentMatchDto
                {
                    Id = m.Id,                  
                    Player1Id = m.Player1Id,
                    Player2Id = m.Player2Id,
                    WinnerId = m.WinnerId,
                    NextMatchId = m.NextMatchId,
                    NextMatchPosition = m.NextMatchPosition,
                    Date = m.Date,
                    IsPlayed = m.IsPlayed,
                    Player1Deck = m.Player1Id.HasValue && tournamentPlayers.ContainsKey(m.Player1Id.Value)
                        ? tournamentPlayers[m.Player1Id.Value]
                        : null,
                    Player2Deck = m.Player2Id.HasValue && tournamentPlayers.ContainsKey(m.Player2Id.Value)
                        ? tournamentPlayers[m.Player2Id.Value]
                        : null
                }).ToList() ?? new List<TournamentMatchDto>()
            }).ToList() ?? new List<BracketDto>()
        };

        return Ok(dto);
    }

    [HttpGet("{id}/participants")]
    public async Task<IActionResult> GetParticipants(int id)
    {
        var participants = await _context.TournamentPlayers
            .Where(tp => tp.TournamentId == id)
            .Include(tp => tp.Player)
            .Select(tp => new { tp.PlayerId, PlayerName = tp.Player.Name, tp.Deck })
            .ToListAsync();
        return Ok(participants);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTournament(int id)
    {
        var tournament = await _context.Tournaments
            .Include(t => t.Brackets)
                .ThenInclude(b => b.Matches)
            .Include(t => t.TournamentPlayers)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tournament == null)
            return NotFound();

        // Remove primeiro os TournamentPlayers (participantes)
        _context.TournamentPlayers.RemoveRange(tournament.TournamentPlayers);

        // Em seguida, os Matches de cada Bracket
        foreach (var bracket in tournament.Brackets)
        {
            _context.TournamentMatches.RemoveRange(bracket.Matches);
        }
        // Remove os Brackets
        //_context.Brackets.RemoveRange(tournament.Brackets);
        // Finalmente, remove o Torneio
        _context.Tournaments.Remove(tournament);

        await _context.SaveChangesAsync();
        return NoContent();
    }

    //POST: api/tournament/{id}/generate-brackets
    //[HttpPost("{id}/generate-brackets")]
    //public async Task<IActionResult> GenerateBrackets(int id)
    //{
    //    try
    //    {
    //        await _tournamentService.GenerateBrackets(id);
    //        return Ok(new { message = "Chaveamento gerado com sucesso." });
    //    }
    //    catch (Exception ex)
    //    {
    //        return BadRequest(new { error = ex.Message });
    //    }
    //}

    [HttpPost("{id}/generate-double-elimination")]
    public async Task<IActionResult> GenerateDoubleElimination(int id)
    {
        // Busca os IDs dos participantes do torneio
        var playerIds = await _context.TournamentPlayers
            .Where(tp => tp.TournamentId == id)
            .Select(tp => tp.PlayerId)
            .ToListAsync();

        int count = playerIds.Count;

        if (playerIds.Count < 2)
            return BadRequest(new { error = "Número insuficiente de participantes (mínimo 2)." });
        if ((count & (count - 1)) != 0)  // verifica se é potência de 2
        {
            return BadRequest(new { error = "O número de participantes deve ser potência de 2 (2, 4, 8, 16, 32...)." });
        }

        try
        {
            await _tournamentService.GenerateDoubleElimination(id, playerIds);
            return Ok(new { message = "Chaveamento de dupla eliminação gerado com sucesso." });
        }
        catch (DbUpdateException dbEx)
        {
            var inner = dbEx.InnerException?.Message;
            return BadRequest(new { error = $"Erro ao salvar: {inner}" });
        }
    }

    [HttpGet("{id}/matches")]
    public async Task<ActionResult<IEnumerable<TournamentMatch>>> GetTournamentMatches(int id)
    {
        var matches = await _context.TournamentMatches
            .Where(m => m.TournamentId == id)
            .OrderBy(m => m.MatchType).ThenBy(m => m.Round).ThenBy(m => m.Id)
            .ToListAsync();
        return Ok(matches);
    }
}
