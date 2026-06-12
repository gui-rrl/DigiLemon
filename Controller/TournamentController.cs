using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RankingDigi.Data;
using RankingDigi.DTOs;
using RankingDigi.Models;
using RankingDigi.Services;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TournamentController : ControllerBase
{
    private readonly RankingContext _context;
    private readonly TournamentService _tournamentService;
    private readonly SwissService _swissService;

    public TournamentController(RankingContext context, TournamentService tournamentService, SwissService swissService)
    {
        _context = context;
        _tournamentService = tournamentService;
        _swissService = swissService;
    }

   //GET: api/tournament
   [HttpGet]
   [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<TournamentDto>>> GetTournaments()
    {
        var tournaments = await _context.Tournaments
            .Include(t => t.Brackets)
                .ThenInclude(b => b.Matches)
            .Include(t => t.TournamentPlayers)
                .ThenInclude(tp => tp.Player)
            .ToListAsync();

        // Carrega grand finals direto de TournamentMatches (gerador salva sem Bracket)
        var tournamentIds = tournaments.Select(t => t.Id).ToList();
        var grandFinalMatches = await _context.TournamentMatches
            .Where(m => tournamentIds.Contains(m.TournamentId) && m.MatchType == 2 && m.WinnerId != null)
            .ToListAsync();

        // Backfill: marca como Finalizado torneios cuja Grand Final já tem vencedor
        bool changed = false;
        var finishedIds = grandFinalMatches.Select(m => m.TournamentId).ToHashSet();
        foreach (var t in tournaments.Where(x => x.Status != 2 && finishedIds.Contains(x.Id)))
        {
            t.Status = 2;
            changed = true;
        }

        // Backfill: gera InviteCode para torneios antigos
        foreach (var t in tournaments.Where(x => string.IsNullOrEmpty(x.InviteCode)))
        {
            t.InviteCode = await GenerateUniqueInviteCodeAsync();
            changed = true;
        }
        if (changed) await _context.SaveChangesAsync();

        // Pré-calcula campeão dos torneios Swiss Pontos Corridos finalizados (usa OMW% como desempate)
        var purSwissFinishedIds = tournaments
            .Where(t => t.Format == 2 && t.Status == 2)
            .Select(t => t.Id)
            .ToList();
        var pureSwissWinners = new Dictionary<int, (string? Name, string? AvatarUrl)>();
        foreach (var tid in purSwissFinishedIds)
        {
            var standings = await _swissService.GetStandingsAsync(tid);
            var top = standings.FirstOrDefault();
            if (top != null)
            {
                string? avatarUrl = null;
                if (top.PlayerId.HasValue)
                {
                    var topPlayer = await _context.Players.FindAsync(top.PlayerId.Value);
                    avatarUrl = topPlayer?.AvatarUrl;
                }
                pureSwissWinners[tid] = (top.PlayerName, avatarUrl);
            }
        }

        var dtos = tournaments.Select(t =>
        {
            // Campeão: Grand Final (Double Elim / Swiss+TopCut) ou 1º lugar Swiss puro (com OMW%)
            var grandFinalWinnerId = grandFinalMatches
                .FirstOrDefault(m => m.TournamentId == t.Id)?.WinnerId;
            var grandFinalWinnerTp = grandFinalWinnerId.HasValue
                ? t.TournamentPlayers?.FirstOrDefault(p => p.Id == grandFinalWinnerId)
                : null;
            string? winnerName = grandFinalWinnerTp?.DisplayName;
            string? winnerAvatarUrl = grandFinalWinnerTp?.Player?.AvatarUrl;
            if (winnerName == null && pureSwissWinners.TryGetValue(t.Id, out var sw))
            {
                winnerName = sw.Name;
                winnerAvatarUrl = sw.AvatarUrl;
            }

            return new TournamentDto
            {
            Id = t.Id,
            Name = t.Name,
            StartDate = t.StartDate,
            Status = t.Status,
            InviteCode = t.InviteCode,
            MaxPlayers = t.MaxPlayers,
            Format = t.Format,
            SwissRounds = t.SwissRounds,
            TopCutSize = t.TopCutSize,
            CurrentSwissRound = t.CurrentSwissRound,
            WinnerName = winnerName,
            WinnerAvatarUrl = winnerAvatarUrl,
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
            };
        }).ToList();

        return Ok(dtos);
    }

    //POST: api/tournament
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateTournament([FromBody] CreateTournamentDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { error = "Informe o nome do torneio." });

        if (dto.MaxPlayers < 2)
            return BadRequest(new { error = "O número máximo de jogadores deve ser maior ou igual a 2." });
        if (dto.Format == 0 && dto.MaxPlayers % 2 != 0)
            return BadRequest(new { error = "Para o formato Double Elimination, o número de vagas deve ser par." });

        var players = dto.Players ?? new List<PlayerDeckDto>();

        if (players.Count > dto.MaxPlayers)
            return BadRequest(new { error = $"Você adicionou {players.Count} jogadores, mas o limite do torneio é {dto.MaxPlayers}." });

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

        int swissRounds = dto.Format >= 1 ? SwissService.CalculateRounds(dto.MaxPlayers) : 0;
        int topCutSize  = dto.Format == 1 ? (dto.TopCutSize is 4 or 8 ? dto.TopCutSize : 8) : 0;

        var tournament = new Tournament
        {
            Name = dto.Name,
            StartDate = dto.StartDate,
            Status = 0,
            MaxPlayers = dto.MaxPlayers,
            InviteCode = await GenerateUniqueInviteCodeAsync(),
            Format = dto.Format,
            SwissRounds = swissRounds,
            TopCutSize = topCutSize,
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

    //GET: api/tournament/invite/{code} - rota pública
    [HttpGet("invite/{code}")]
    [AllowAnonymous]
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
            tournament.MaxPlayers,
            Participants = participants,
            IsOpenForJoin = tournament.Status == 0 && (tournament.MaxPlayers == 0 || participants.Count < tournament.MaxPlayers),
        });
    }

    //POST: api/tournament/invite/{code}/join - rota pública
    [HttpPost("invite/{code}/join")]
    [AllowAnonymous]
    public async Task<IActionResult> JoinByInvite(string code, [FromBody] JoinTournamentDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Deck))
            return BadRequest(new { error = "Informe o nome e o deck para ingressar no torneio." });

        var tournament = await _context.Tournaments.FirstOrDefaultAsync(t => t.InviteCode == code);
        if (tournament == null)
            return NotFound(new { error = "Torneio não encontrado." });
        if (tournament.Status != 0)
            return BadRequest(new { error = "Este torneio já foi iniciado. Novos jogadores não podem mais ingressar." });

        if (tournament.MaxPlayers > 0)
        {
            var currentCount = await _context.TournamentPlayers.CountAsync(tp => tp.TournamentId == tournament.Id);
            if (currentCount >= tournament.MaxPlayers)
                return BadRequest(new { error = $"Este torneio já está cheio ({tournament.MaxPlayers} vagas preenchidas)." });
        }

        var guestName = dto.Name.Trim();
        var deck      = dto.Deck.Trim();

        // Convidados via link NÃO são registrados na tabela Players.
        // Verifica duplicata pelo nome dentro do torneio.
        bool alreadyIn = await _context.TournamentPlayers
            .AnyAsync(tp => tp.TournamentId == tournament.Id &&
                            (tp.GuestName != null && tp.GuestName.ToLower() == guestName.ToLower() ||
                             tp.Player    != null && tp.Player.Name.ToLower() == guestName.ToLower()));
        if (alreadyIn)
            return Conflict(new { error = $"\"{guestName}\" já está inscrito neste torneio." });

        _context.TournamentPlayers.Add(new TournamentPlayer
        {
            TournamentId = tournament.Id,
            PlayerId     = null,        // guest: sem vínculo com Player
            GuestName    = guestName,
            Deck         = deck,
        });
        await _context.SaveChangesAsync();

        return Ok(new
        {
            tournamentName = tournament.Name,
            playerName     = guestName,
            message        = $"Você foi inscrito como \"{guestName}\" com o deck \"{deck}\".",
        });
    }

    //DELETE: api/tournament/{id}/participants/{tpId} - remove participante por TournamentPlayer.Id
    [HttpDelete("{id}/participants/{tpId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RemoveParticipant(int id, int tpId)
    {
        var tournament = await _context.Tournaments.FindAsync(id);
        if (tournament == null) return NotFound(new { error = "Torneio não encontrado." });
        if (tournament.Status != 0)
            return BadRequest(new { error = "Não é possível remover participantes após o torneio ser iniciado." });

        var participation = await _context.TournamentPlayers
            .FirstOrDefaultAsync(tp => tp.TournamentId == id && tp.Id == tpId);
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

        // Carrega participantes keyed por TournamentPlayer.Id (usado no chaveamento)
        var tournamentPlayers = await _context.TournamentPlayers
            .Where(tp => tp.TournamentId == id)
            .Include(tp => tp.Player)
            .ToDictionaryAsync(tp => tp.Id, tp => new { tp.Deck, Name = tp.GuestName ?? tp.Player?.Name ?? "Desconhecido" });

        var dto = new TournamentDto
        {
            Id = tournament.Id,
            Name = tournament.Name,
            StartDate = tournament.StartDate,
            Status = tournament.Status,
            InviteCode = tournament.InviteCode,
            MaxPlayers = tournament.MaxPlayers,
            Format = tournament.Format,
            SwissRounds = tournament.SwissRounds,
            TopCutSize = tournament.TopCutSize,
            CurrentSwissRound = tournament.CurrentSwissRound,
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
                        ? tournamentPlayers[m.Player1Id.Value].Deck
                        : null,
                    Player2Deck = m.Player2Id.HasValue && tournamentPlayers.ContainsKey(m.Player2Id.Value)
                        ? tournamentPlayers[m.Player2Id.Value].Deck
                        : null
                }).ToList() ?? new List<TournamentMatchDto>()
            }).ToList() ?? new List<BracketDto>()
        };

        return Ok(dto);
    }

    [HttpGet("{id}/participants")]
    [AllowAnonymous]
    public async Task<IActionResult> GetParticipants(int id)
    {
        // ToListAsync primeiro garante que .Include(Player) seja respeitado pelo EF Core.
        // Projeção em memória evita NullReferenceException em tp.Player dentro de .Select().
        var tps = await _context.TournamentPlayers
            .Where(tp => tp.TournamentId == id)
            .Include(tp => tp.Player)
            .ToListAsync();

        var result = tps.Select(tp => new
        {
            Id         = tp.Id,
            tp.PlayerId,
            PlayerName = tp.GuestName ?? tp.Player?.Name ?? "Desconhecido",
            IsGuest    = tp.PlayerId == null,
            tp.Deck,
        });

        return Ok(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
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
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GenerateDoubleElimination(int id)
    {
        // Usa TournamentPlayer.Id como identificador no chaveamento (suporta guests)
        var playerIds = await _context.TournamentPlayers
            .Where(tp => tp.TournamentId == id)
            .Select(tp => tp.Id)
            .ToListAsync();

        int count = playerIds.Count;

        if (playerIds.Count < 2)
            return BadRequest(new { error = "Número insuficiente de participantes (mínimo 2)." });
        if (count % 2 != 0)
            return BadRequest(new { error = "O número de participantes deve ser par." });

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

    // ── Swiss endpoints ──────────────────────────────────────────────────────

    [HttpPost("{id}/swiss/start")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SwissStart(int id)
    {
        try
        {
            await _swissService.StartAsync(id);
            return Ok(new { message = "Swiss iniciado. Rodada 1 gerada." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id}/swiss/advance")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SwissAdvance(int id)
    {
        try
        {
            await _swissService.AdvanceRoundAsync(id);
            var t = await _context.Tournaments.FindAsync(id);
            return Ok(new { message = $"Rodada {t!.CurrentSwissRound} gerada.", round = t.CurrentSwissRound });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id}/swiss/finish")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SwissFinish(int id)
    {
        try
        {
            await _swissService.FinishAsync(id);
            return Ok(new { message = "Torneio encerrado com sucesso." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id}/swiss/generate-topcut")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SwissGenerateTopCut(int id)
    {
        try
        {
            await _swissService.GenerateTopCutAsync(id);
            return Ok(new { message = "Top Cut gerado com sucesso." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("{id}/swiss/standings")]
    [AllowAnonymous]
    public async Task<IActionResult> SwissStandings(int id)
    {
        var standings = await _swissService.GetStandingsAsync(id);
        return Ok(standings);
    }

    [HttpGet("{id}/swiss/status")]
    [AllowAnonymous]
    public async Task<IActionResult> SwissStatus(int id)
    {
        var tournament = await _context.Tournaments.FindAsync(id);
        if (tournament == null) return NotFound();

        var allMatches = await _context.TournamentMatches
            .Where(m => m.TournamentId == id)
            .OrderBy(m => m.Round).ThenBy(m => m.Id)
            .ToListAsync();

        var swissMatches = allMatches.Where(m => m.MatchType == 3).ToList();
        var topCutMatches = allMatches.Where(m => m.MatchType != 3).ToList();

        var standings = await _swissService.GetStandingsAsync(id);

        int currentRound = tournament.CurrentSwissRound;
        bool allSwissDone = currentRound > 0 && currentRound >= tournament.SwissRounds
            && swissMatches.Where(m => m.Round == tournament.SwissRounds).All(m => m.IsPlayed);

        bool currentRoundDone = currentRound > 0
            && swissMatches.Where(m => m.Round == currentRound).All(m => m.IsPlayed);

        bool topCutGenerated = topCutMatches.Any();

        return Ok(new
        {
            tournament.Format,
            tournament.SwissRounds,
            tournament.TopCutSize,
            tournament.CurrentSwissRound,
            tournament.Status,
            AllSwissDone       = allSwissDone,
            CurrentRoundDone   = currentRoundDone,
            TopCutGenerated    = topCutGenerated,
            Standings          = standings,
            SwissMatchesByRound = swissMatches
                .GroupBy(m => m.Round)
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.OrderBy(m => m.Id).Select(m => new
                {
                    m.Id, m.Player1Id, m.Player2Id, m.WinnerId,
                    m.IsPlayed, m.IsBye, m.Date,
                }).ToList()),
            TopCutMatches = topCutMatches.Select(m => new
            {
                m.Id, m.MatchType, m.Round,
                m.Player1Id, m.Player2Id, m.WinnerId,
                m.IsPlayed, m.IsBye,
                m.NextMatchId, m.NextMatchPosition,
                m.LoserGoesToMatchId, m.Date,
            }).ToList(),
        });
    }
}
