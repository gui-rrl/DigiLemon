using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RankingDigi.Data;
using RankingDigi.DTOs;
using RankingDigi.Models;
using RankingDigi.Services;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TournamentController : ControllerBase
{
    private readonly RankingContext _context;
    private readonly TournamentService _tournamentService;
    private readonly SwissService _swissService;
    private readonly MatchReportCodeService _reportCodes;

    public TournamentController(RankingContext context, TournamentService tournamentService, SwissService swissService, MatchReportCodeService reportCodes)
    {
        _context = context;
        _tournamentService = tournamentService;
        _swissService = swissService;
        _reportCodes = reportCodes;
    }

   //GET: api/tournament
   [HttpGet]
   [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<TournamentDto>>> GetTournaments()
    {
        int? myPlayerId = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            myPlayerId = (await _context.AppUsers.FindAsync(userId))?.PlayerId;
        }

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

        // Campeão de cada torneio: Grand Final (Double Elim / Swiss+TopCut) ou 1º lugar Swiss puro (com OMW%)
        var winners = await _tournamentService.GetTournamentWinnersAsync(tournaments, _swissService);

        var dtos = tournaments.Select(t =>
        {
            winners.TryGetValue(t.Id, out var winner);
            string? winnerName = winner?.Name;
            string? winnerAvatarUrl = winner?.AvatarUrl;

            return new TournamentDto
            {
            Id = t.Id,
            Name = t.Name,
            StartDate = t.StartDate,
            EndDate = t.EndDate,
            RegistrationDeadline = t.RegistrationDeadline,
            Status = t.Status,
            InviteCode = t.InviteCode,
            MaxPlayers = t.MaxPlayers,
            Mode = t.Mode,
            Format = t.Format,
            SwissRounds = t.SwissRounds,
            TopCutSize = t.TopCutSize,
            CurrentSwissRound = t.CurrentSwissRound,
            DeckMode = t.DeckMode,
            DeckPoolSize = t.DeckPoolSize,
            WinnerName = winnerName,
            WinnerAvatarUrl = winnerAvatarUrl,
            TrophyImageUrl = t.TrophyImageUrl,
            MyParticipationId = myPlayerId.HasValue
                ? t.TournamentPlayers?.FirstOrDefault(tp => tp.PlayerId == myPlayerId.Value)?.Id
                : null,
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

    //GET: api/tournament/trophies — galeria pública dos troféus (torneios com imagem de troféu + campeão definido)
    [HttpGet("trophies")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTrophies()
    {
        var tournaments = await _context.Tournaments
            .Where(t => t.TrophyImageUrl != null)
            .Include(t => t.TournamentPlayers)
                .ThenInclude(tp => tp.Player)
            .ToListAsync();

        var winners = await _tournamentService.GetTournamentWinnersAsync(tournaments, _swissService);

        var trophies = tournaments
            .Select(t =>
            {
                winners.TryGetValue(t.Id, out var winner);
                return new
                {
                    TournamentId = t.Id,
                    TournamentName = t.Name,
                    TrophyImageUrl = t.TrophyImageUrl,
                    WinnerName = winner?.Name,
                    WinnerAvatarUrl = winner?.AvatarUrl,
                };
            })
            .Where(x => x.WinnerName != null)
            .OrderByDescending(x => x.TournamentId)
            .ToList();

        return Ok(trophies);
    }

    // As inscrições ficam abertas durante todo o dia do prazo e fecham à meia-noite seguinte
    // (mesma convenção da EndDate). Usa hora local porque a data vem do date picker do navegador,
    // que é local — assim "meia-noite" é a meia-noite que o organizador vê.
    private static bool IsRegistrationExpired(Tournament t) =>
        t.RegistrationDeadline.HasValue && DateTime.Now >= t.RegistrationDeadline.Value.Date.AddDays(1);

    //POST: api/tournament
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateTournament([FromBody] CreateTournamentDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { error = "Informe o nome do torneio." });

        if (dto.Format is < 0 or > 3)
            return BadRequest(new { error = "Formato de torneio inválido." });
        // MaxPlayers == 0 é o sentinela de "Indefinido" (vagas abertas até o prazo de inscrição
        // vencer ou o admin encerrar manualmente) — reaproveita a convenção já usada em
        // GetByInviteCode/JoinByInvite (tournament.MaxPlayers > 0 já trata 0 como "sem teto").
        bool isUnlimited = dto.MaxPlayers == 0;
        if (!isUnlimited && dto.MaxPlayers < 2)
            return BadRequest(new { error = "O número máximo de jogadores deve ser maior ou igual a 2 (ou 0 para Indefinido)." });
        if (dto.Format == 0 && dto.MaxPlayers % 2 != 0)
            return BadRequest(new { error = "Para o formato Double Elimination, o número de vagas deve ser par." });

        // Data de término só é obrigatória com vagas fixas. Em modo Indefinido ela é opcional
        // (o torneio pode não ter fim previsto) e a data de início não é escolhida pelo admin —
        // o frontend preenche com a data de hoje.
        if (!isUnlimited && !dto.EndDate.HasValue)
            return BadRequest(new { error = "Informe a data de término do torneio." });
        if (dto.EndDate.HasValue && dto.EndDate.Value.Date < dto.StartDate.Date)
            return BadRequest(new { error = "A data de término não pode ser anterior à data de início." });

        if (dto.RegistrationDeadline.HasValue && dto.EndDate.HasValue && dto.RegistrationDeadline.Value.Date > dto.EndDate.Value.Date)
            return BadRequest(new { error = "O prazo de inscrição não pode ser depois da data de término do torneio." });

        // ── Modo de deck (Sorteio entre decks próprios / Death Random) ────────────────────
        if (dto.DeckMode is < 0 or > 2)
            return BadRequest(new { error = "Modo de deck inválido." });
        if (dto.DeckMode != 0 && dto.DeckPoolSize is < 1 or > 3)
            return BadRequest(new { error = "A quantidade de decks no sorteio deve ser 1, 2 ou 3." });
        if (dto.DeckMode == 2 && dto.Format == 2)
            return BadRequest(new { error = "Death Random não é compatível com Swiss Pontos Corridos. Escolha Dupla Eliminação, Swiss+TopCut ou Todos contra todos+TopCut." });

        var players = dto.Players ?? new List<PlayerDeckDto>();

        // O sorteio exige que os decks sejam enviados como "pool" (TournamentPlayerDeckOption),
        // não resolvidos na hora — por isso não dá pra pré-cadastrar participantes já na criação
        // quando o modo de deck exige sorteio. O torneio nasce vazio e a inscrição acontece
        // depois, pelo link de convite ou pela tela de configuração (ambos já suportam múltiplos
        // decks quando DeckMode != 0).
        if (dto.DeckMode != 0 && players.Count > 0)
            return BadRequest(new { error = "Não é possível pré-cadastrar participantes na criação quando o modo de deck exige sorteio. Crie o torneio vazio e inscreva pelo link de convite ou pela tela de configuração." });

        if (!isUnlimited && players.Count > dto.MaxPlayers)
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

        // Todos contra todos (3): uma "rodada" única com todas as partidas, então SwissRounds = 1.
        // Indefinido (isUnlimited): fica 0 de propósito — SwissService.StartAsync já recalcula a
        // partir da contagem real de participantes no momento de iniciar (SwissService.cs:54-56).
        int swissRounds = isUnlimited ? 0
                        : dto.Format == 3 ? 1
                        : dto.Format >= 1 ? SwissService.CalculateRounds(dto.MaxPlayers)
                        : 0;
        int topCutSize  = dto.Format is 1 or 3 ? (dto.TopCutSize is 4 or 8 ? dto.TopCutSize : 4) : 0;

        var tournament = new Tournament
        {
            Name = dto.Name,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            RegistrationDeadline = dto.RegistrationDeadline,
            Status = 0,
            MaxPlayers = dto.MaxPlayers,
            Mode = dto.Mode,
            InviteCode = await GenerateUniqueInviteCodeAsync(),
            Format = dto.Format,
            SwissRounds = swissRounds,
            TopCutSize = topCutSize,
            DeckMode = dto.DeckMode,
            DeckPoolSize = dto.DeckMode == 0 ? 1 : dto.DeckPoolSize,
        };
        _context.Tournaments.Add(tournament);
        await _context.SaveChangesAsync();

        if (players.Count > 0)
        {
            var tournamentPlayers = new List<TournamentPlayer>();
            foreach (var p in players)
            {
                var deckName = p.Deck;
                if (p.DeckId.HasValue)
                {
                    var savedDeck = await _context.Decks.FindAsync(p.DeckId.Value);
                    if (savedDeck != null && savedDeck.PlayerId == p.PlayerId)
                        deckName = savedDeck.Name;
                }
                tournamentPlayers.Add(new TournamentPlayer
                {
                    TournamentId = tournament.Id,
                    PlayerId = p.PlayerId,
                    Deck = deckName,
                    DeckId = p.DeckId,
                });
            }
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
            .Select(tp => new { tp.PlayerId, PlayerName = tp.GuestName ?? tp.Player!.Name, tp.Deck })
            .ToListAsync();

        // Motivo do fechamento na ordem em que importa pra quem abre o convite:
        // torneio já iniciado > prazo de inscrição vencido > vagas esgotadas.
        string? closedReason =
            tournament.Status != 0                                                        ? "started"
            : IsRegistrationExpired(tournament)                                           ? "deadline"
            : tournament.MaxPlayers > 0 && participants.Count >= tournament.MaxPlayers    ? "full"
            : null;

        return Ok(new
        {
            tournament.Id,
            tournament.Name,
            tournament.StartDate,
            tournament.RegistrationDeadline,
            tournament.Status,
            tournament.InviteCode,
            tournament.MaxPlayers,
            tournament.DeckMode,
            tournament.DeckPoolSize,
            Participants = participants,
            IsOpenForJoin = closedReason == null,
            ClosedReason = closedReason,
        });
    }

    //POST: api/tournament/invite/{code}/join - exige login + jogador vinculado + deck próprio
    [HttpPost("invite/{code}/join")]
    [Authorize]
    public async Task<IActionResult> JoinByInvite(string code, [FromBody] JoinTournamentDto dto)
    {
        if (dto == null)
            return BadRequest(new { error = "Escolha um deck para ingressar no torneio." });

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user   = await _context.AppUsers.Include(u => u.Player).FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return Unauthorized();
        if (!user.PlayerId.HasValue || user.Player == null)
            return BadRequest(new { error = "Sua conta ainda não tem um jogador vinculado." });

        var tournament = await _context.Tournaments.FirstOrDefaultAsync(t => t.InviteCode == code);
        if (tournament == null)
            return NotFound(new { error = "Torneio não encontrado." });
        if (tournament.Status != 0)
            return BadRequest(new { error = "Este torneio já foi iniciado. Novos jogadores não podem mais ingressar." });
        if (IsRegistrationExpired(tournament))
            return BadRequest(new { error = $"As inscrições encerraram em {tournament.RegistrationDeadline!.Value:dd/MM/yyyy}." });

        if (tournament.MaxPlayers > 0)
        {
            var currentCount = await _context.TournamentPlayers.CountAsync(tp => tp.TournamentId == tournament.Id);
            if (currentCount >= tournament.MaxPlayers)
                return BadRequest(new { error = $"Este torneio já está cheio ({tournament.MaxPlayers} vagas preenchidas)." });
        }

        var playerId = user.PlayerId.Value;
        bool alreadyIn = await _context.TournamentPlayers
            .AnyAsync(tp => tp.TournamentId == tournament.Id && tp.PlayerId == playerId);
        if (alreadyIn)
            return Conflict(new { error = $"\"{user.Player.Name}\" já está inscrito neste torneio." });

        var bannedDeckIds = await DeckBanService.GetBannedDeckIdsAsync(_context);

        // ── DeckMode 0 (normal): um único deck, resolvido na hora — comportamento de sempre ──
        if (tournament.DeckMode == 0)
        {
            if (dto.DeckId <= 0)
                return BadRequest(new { error = "Escolha um deck para ingressar no torneio." });

            var deck = await _context.Decks.FindAsync(dto.DeckId);
            if (deck == null || deck.PlayerId != user.PlayerId.Value)
                return BadRequest(new { error = "Deck não encontrado ou não pertence a você." });

            if (bannedDeckIds.Contains(deck.Id))
                return BadRequest(new { error = $"O deck \"{deck.Name}\" ficou entre os 4 primeiros do último Top Cut e está banido de disputar o próximo torneio. Escolha outro deck." });

            _context.TournamentPlayers.Add(new TournamentPlayer
            {
                TournamentId = tournament.Id,
                PlayerId     = playerId,
                Deck         = deck.Name,
                DeckId       = deck.Id,
            });
            await _context.SaveChangesAsync();

            return Ok(new
            {
                tournamentName = tournament.Name,
                playerName     = user.Player.Name,
                message        = $"Você foi inscrito como \"{user.Player.Name}\" com o deck \"{deck.Name}\".",
            });
        }

        // ── DeckMode 1/2: envia DeckPoolSize decks, o sorteio define o deck oficial ao iniciar ──
        var deckIds = dto.DeckIds ?? new List<int>();
        if (deckIds.Count != tournament.DeckPoolSize)
            return BadRequest(new { error = $"Escolha exatamente {tournament.DeckPoolSize} deck(s) para este torneio." });
        if (deckIds.Distinct().Count() != deckIds.Count)
            return BadRequest(new { error = "Não é possível repetir o mesmo deck." });

        var poolDecks = await _context.Decks.Where(d => deckIds.Contains(d.Id)).ToListAsync();
        if (poolDecks.Count != deckIds.Count || poolDecks.Any(d => d.PlayerId != user.PlayerId.Value))
            return BadRequest(new { error = "Um ou mais decks não foram encontrados ou não pertencem a você." });

        var bannedNames = poolDecks.Where(d => bannedDeckIds.Contains(d.Id)).Select(d => d.Name).ToList();
        if (bannedNames.Count > 0)
            return BadRequest(new { error = $"O(s) deck(s) \"{string.Join(", ", bannedNames)}\" estão banidos de disputar o próximo torneio." });

        var tp = new TournamentPlayer { TournamentId = tournament.Id, PlayerId = playerId, Deck = null, DeckId = null };
        _context.TournamentPlayers.Add(tp);
        await _context.SaveChangesAsync();

        _context.TournamentPlayerDeckOptions.AddRange(poolDecks.Select(d => new TournamentPlayerDeckOption
        {
            TournamentPlayerId = tp.Id,
            DeckId = d.Id,
        }));
        await _context.SaveChangesAsync();

        return Ok(new
        {
            tournamentName = tournament.Name,
            playerName     = user.Player.Name,
            message        = $"Você foi inscrito como \"{user.Player.Name}\". Seus decks entram no sorteio quando o torneio iniciar.",
        });
    }

    //POST: api/tournament/{id}/participants - Admin inscreve um jogador manualmente.
    //Ao contrário do link de convite, não exige que o jogador tenha conta nem deck salvo, e
    //ignora de propósito o prazo de inscrição: é a via do organizador para casos fora do fluxo
    //normal (inscrição presencial, jogador sem conta no sistema, atraso combinado).
    [HttpPost("{id}/participants")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddParticipant(int id, [FromBody] PlayerDeckDto dto)
    {
        if (dto == null || dto.PlayerId <= 0)
            return BadRequest(new { error = "Selecione um jogador." });

        var tournament = await _context.Tournaments.FindAsync(id);
        if (tournament == null) return NotFound(new { error = "Torneio não encontrado." });
        if (tournament.Status != 0)
            return BadRequest(new { error = "Não é possível inscrever participantes após o torneio ser iniciado." });

        var player = await _context.Players.FindAsync(dto.PlayerId);
        if (player == null) return NotFound(new { error = "Jogador não encontrado." });

        bool alreadyIn = await _context.TournamentPlayers
            .AnyAsync(tp => tp.TournamentId == id && tp.PlayerId == dto.PlayerId);
        if (alreadyIn)
            return Conflict(new { error = $"\"{player.Name}\" já está inscrito neste torneio." });

        if (tournament.MaxPlayers > 0)
        {
            var currentCount = await _context.TournamentPlayers.CountAsync(tp => tp.TournamentId == id);
            if (currentCount >= tournament.MaxPlayers)
                return BadRequest(new { error = $"Este torneio já está cheio ({tournament.MaxPlayers} vagas preenchidas)." });
        }

        // ── DeckMode 1/2: exige DeckPoolSize decks salvos deste jogador (sem fallback de texto
        // livre — o sorteio/clonagem exige um Deck real com cartas) ──────────────────────────
        if (tournament.DeckMode != 0)
        {
            var poolDeckIds = dto.DeckIds ?? new List<int>();
            if (poolDeckIds.Count != tournament.DeckPoolSize)
                return BadRequest(new { error = $"Selecione exatamente {tournament.DeckPoolSize} deck(s) salvo(s) deste jogador." });
            if (poolDeckIds.Distinct().Count() != poolDeckIds.Count)
                return BadRequest(new { error = "Não é possível repetir o mesmo deck." });

            var poolDecks = await _context.Decks.Where(d => poolDeckIds.Contains(d.Id)).ToListAsync();
            if (poolDecks.Count != poolDeckIds.Count || poolDecks.Any(d => d.PlayerId != dto.PlayerId))
                return BadRequest(new { error = "Um ou mais decks não pertencem a esse jogador." });

            var bannedDeckIds = await DeckBanService.GetBannedDeckIdsAsync(_context);
            var bannedNames = poolDecks.Where(d => bannedDeckIds.Contains(d.Id)).Select(d => d.Name).ToList();
            if (bannedNames.Count > 0)
                return BadRequest(new { error = $"O(s) deck(s) \"{string.Join(", ", bannedNames)}\" estão banidos de disputar o próximo torneio." });

            var pending = new TournamentPlayer { TournamentId = id, PlayerId = dto.PlayerId, Deck = null, DeckId = null };
            _context.TournamentPlayers.Add(pending);
            await _context.SaveChangesAsync();

            _context.TournamentPlayerDeckOptions.AddRange(poolDecks.Select(d => new TournamentPlayerDeckOption
            {
                TournamentPlayerId = pending.Id,
                DeckId = d.Id,
            }));
            await _context.SaveChangesAsync();

            return Ok(new { pending.Id, PlayerId = dto.PlayerId, PlayerName = player.Name, PendingDraw = true });
        }

        // Se o admin escolheu um deck salvo, o nome vem do próprio deck; senão vale o texto digitado.
        var deckName = dto.Deck?.Trim();
        int? deckId = null;
        if (dto.DeckId.HasValue)
        {
            var savedDeck = await _context.Decks.FindAsync(dto.DeckId.Value);
            if (savedDeck == null || savedDeck.PlayerId != dto.PlayerId)
                return BadRequest(new { error = "O deck escolhido não pertence a esse jogador." });

            var bannedDeckIds = await DeckBanService.GetBannedDeckIdsAsync(_context);
            if (bannedDeckIds.Contains(savedDeck.Id))
                return BadRequest(new { error = $"O deck \"{savedDeck.Name}\" ficou entre os 4 primeiros do último Top Cut e está banido de disputar o próximo torneio." });

            deckName = savedDeck.Name;
            deckId   = savedDeck.Id;
        }
        if (string.IsNullOrWhiteSpace(deckName))
            return BadRequest(new { error = "Informe o deck do jogador." });

        var participation = new TournamentPlayer
        {
            TournamentId = id,
            PlayerId     = dto.PlayerId,
            Deck         = deckName,
            DeckId       = deckId,
        };
        _context.TournamentPlayers.Add(participation);
        await _context.SaveChangesAsync();

        return Ok(new { participation.Id, PlayerId = dto.PlayerId, PlayerName = player.Name, Deck = deckName, DeckId = deckId });
    }

    //DELETE: api/tournament/{id}/participants/{tpId} - Admin remove qualquer participante;
    //jogador (não-admin) só pode remover a própria inscrição.
    [HttpDelete("{id}/participants/{tpId}")]
    [Authorize]
    public async Task<IActionResult> RemoveParticipant(int id, int tpId)
    {
        var tournament = await _context.Tournaments.FindAsync(id);
        if (tournament == null) return NotFound(new { error = "Torneio não encontrado." });
        if (tournament.Status != 0)
            return BadRequest(new { error = "Não é possível remover participantes após o torneio ser iniciado." });

        var participation = await _context.TournamentPlayers
            .FirstOrDefaultAsync(tp => tp.TournamentId == id && tp.Id == tpId);
        if (participation == null) return NotFound(new { error = "Participante não encontrado." });

        if (!User.IsInRole("Admin"))
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _context.AppUsers.FindAsync(userId);
            if (user?.PlayerId == null || participation.PlayerId != user.PlayerId)
                return Forbid();
        }

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
            EndDate = tournament.EndDate,
            RegistrationDeadline = tournament.RegistrationDeadline,
            Status = tournament.Status,
            InviteCode = tournament.InviteCode,
            MaxPlayers = tournament.MaxPlayers,
            Mode = tournament.Mode,
            Format = tournament.Format,
            SwissRounds = tournament.SwissRounds,
            TopCutSize = tournament.TopCutSize,
            CurrentSwissRound = tournament.CurrentSwissRound,
            DeckMode = tournament.DeckMode,
            DeckPoolSize = tournament.DeckPoolSize,
            TrophyImageUrl = tournament.TrophyImageUrl,
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

    // POST api/tournament/{id}/trophy — Admin only
    [HttpPost("{id}/trophy")]
    public async Task<IActionResult> UploadTrophy(int id, IFormFile file)
    {
        if (!User.IsInRole("Admin")) return Forbid();

        if (file == null || file.Length == 0)
            return BadRequest(new { error = "Nenhum arquivo enviado." });

        var allowed = new[] { "image/jpeg", "image/png", "image/webp", "image/gif" };
        if (!allowed.Contains(file.ContentType.ToLower()))
            return BadRequest(new { error = "Formato inválido. Use JPG, PNG, WEBP ou GIF." });

        if (file.Length > 2 * 1024 * 1024)
            return BadRequest(new { error = "A imagem deve ter no máximo 2 MB." });

        var tournament = await _context.Tournaments.FindAsync(id);
        if (tournament == null) return NotFound(new { error = "Torneio não encontrado." });

        var ext = Path.GetExtension(file.FileName).ToLower();
        if (string.IsNullOrEmpty(ext)) ext = ".png";

        var trophyDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "trophies");
        Directory.CreateDirectory(trophyDir);

        // Remove troféu anterior se existir
        if (!string.IsNullOrEmpty(tournament.TrophyImageUrl))
        {
            var oldFile = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", tournament.TrophyImageUrl.TrimStart('/'));
            if (System.IO.File.Exists(oldFile)) System.IO.File.Delete(oldFile);
        }

        var fileName = $"{id}{ext}";
        var filePath = Path.Combine(trophyDir, fileName);
        using (var stream = new FileStream(filePath, FileMode.Create))
            await file.CopyToAsync(stream);

        tournament.TrophyImageUrl = $"/trophies/{fileName}";
        await _context.SaveChangesAsync();
        return Ok(new { trophyImageUrl = tournament.TrophyImageUrl });
    }

    // DELETE api/tournament/{id}/trophy — Admin only
    [HttpDelete("{id}/trophy")]
    public async Task<IActionResult> DeleteTrophy(int id)
    {
        if (!User.IsInRole("Admin")) return Forbid();

        var tournament = await _context.Tournaments.FindAsync(id);
        if (tournament == null) return NotFound(new { error = "Torneio não encontrado." });

        if (!string.IsNullOrEmpty(tournament.TrophyImageUrl))
        {
            var oldFile = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", tournament.TrophyImageUrl.TrimStart('/'));
            if (System.IO.File.Exists(oldFile)) System.IO.File.Delete(oldFile);
            tournament.TrophyImageUrl = null;
            await _context.SaveChangesAsync();
        }

        return NoContent();
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
            AvatarUrl  = tp.Player?.AvatarUrl,
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

        // Devolve ao ranking geral os pontos que este torneio creditou, ANTES de apagar
        // participantes e partidas (é deles que os valores são recalculados). Sem isso, quem
        // venceu no torneio excluído ficava com os pontos para sempre, sem nada que explicasse
        // a diferença no ranking.
        var standingsParaReverter = (tournament.Format == 2 && tournament.Status == 2)
            ? await _swissService.GetStandingsAsync(id)   // bônus do Swiss Pontos Corridos veio daqui
            : null;
        await TournamentScoringService.RevertTournamentAsync(_context, tournament, standingsParaReverter);

        // Remove primeiro os TournamentPlayers (participantes)
        _context.TournamentPlayers.RemoveRange(tournament.TournamentPlayers);

        // Todas as partidas do torneio, buscadas pelo TournamentId. Antes só apagava as ligadas
        // a um Bracket — como Swiss, todos contra todos e top cut gravam a partida direto no
        // torneio (sem Bracket), elas ficavam órfãs no banco depois da exclusão.
        var matches = await _context.TournamentMatches
            .Where(m => m.TournamentId == id)
            .ToListAsync();

        // Zera as referências entre partidas antes de excluir, senão o chaveamento
        // (NextMatchId / LoserGoesToMatchId) barra a exclusão por chave estrangeira.
        foreach (var m in matches)
        {
            m.NextMatchId = null;
            m.LoserGoesToMatchId = null;
        }
        await _context.SaveChangesAsync();

        _context.TournamentMatches.RemoveRange(matches);
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
    public async Task<IActionResult> GetTournamentMatches(int id)
    {
        await _reportCodes.EnsureCodesAsync(id);
        var estados = await _reportCodes.GetReportStatesAsync(id);

        var matches = await _context.TournamentMatches
            .Where(m => m.TournamentId == id)
            .OrderBy(m => m.MatchType).ThenBy(m => m.Round).ThenBy(m => m.Id)
            .ToListAsync();

        // Projeção explícita, NÃO a entidade: TournamentMatch carrega os Player1/2ReportCode,
        // que são credenciais e não podem sair por aqui (este endpoint é lido sem autenticação
        // pela tela de bracket). Os códigos saem só por /my-report-codes, filtrados por usuário.
        return Ok(matches.Select(m => new
        {
            m.Id, m.TournamentId, m.MatchType, m.Round,
            m.Player1Id, m.Player2Id, m.WinnerId,
            m.LoserGoesToMatchId, m.NextMatchId, m.NextMatchPosition,
            m.Date, m.IsPlayed, m.IsBye,
            m.Player1GameWins, m.Player2GameWins,
            ReportState = estados.TryGetValue(m.Id, out var st) ? st : "none",
        }).ToList());
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
    // force=true encerra a fase de pontos antecipadamente, cortando o Top Cut pela
    // classificação atual e descartando as partidas que ainda não foram jogadas.
    public async Task<IActionResult> SwissGenerateTopCut(int id, [FromQuery] bool force = false)
    {
        try
        {
            await _swissService.GenerateTopCutAsync(id, force);
            return Ok(new
            {
                message = force
                    ? "Fase de pontos encerrada e Top Cut gerado pela classificação atual."
                    : "Top Cut gerado com sucesso."
            });
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

    // GET: api/tournament/{id}/my-report-codes — códigos que ESTE usuário pode usar no DCGO.
    // Fica separado dos endpoints de partidas de propósito: aqueles são anônimos e os códigos
    // são credenciais. Aqui exige login e devolve só os slots do próprio jogador (admin vê
    // todos, para conseguir ditar um código a quem perdeu o seu).
    [HttpGet("{id}/my-report-codes")]
    [Authorize]
    public async Task<IActionResult> GetMyReportCodes(int id)
    {
        var tournament = await _context.Tournaments.FindAsync(id);
        if (tournament == null) return NotFound();
        if (tournament.Mode != 1) return Ok(Array.Empty<object>());

        await _reportCodes.EnsureCodesAsync(id);

        bool isAdmin = User.IsInRole("Admin");

        // O admin recebe todos os códigos, então nem precisa descobrir quais slots são dele —
        // duas consultas a menos. O jogador comum precisa (só vê os próprios).
        var meusTpIds = new List<int>();
        if (!isAdmin)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var appUser = await _context.AppUsers.FindAsync(userId);
            if (appUser?.PlayerId == null) return Ok(Array.Empty<object>());

            meusTpIds = await _context.TournamentPlayers
                .Where(tp => tp.TournamentId == id && tp.PlayerId == appUser.PlayerId)
                .Select(tp => tp.Id)
                .ToListAsync();

            if (meusTpIds.Count == 0) return Ok(Array.Empty<object>());
        }

        // Carrega as partidas UMA vez e deriva daqui tanto a lista de códigos quanto os ids
        // das já jogadas (que o cálculo de estado precisa) — evita uma segunda consulta.
        var todas = await _context.TournamentMatches
            .Where(m => m.TournamentId == id)
            .Select(m => new { m.Id, m.IsPlayed, m.IsBye, m.Player1Id, m.Player2Id, m.Player1ReportCode, m.Player2ReportCode })
            .ToListAsync();

        var jogadas = todas.Where(m => m.IsPlayed).Select(m => m.Id).ToHashSet();
        var matches = todas.Where(m => !m.IsPlayed && !m.IsBye
                                    && m.Player1ReportCode != null && m.Player2ReportCode != null).ToList();

        if (matches.Count == 0) return Ok(Array.Empty<object>());

        var estados = await _reportCodes.GetReportStatesAsync(id, jogadas);

        var resultado = new List<object>();
        foreach (var m in matches)
        {
            var estado = estados.TryGetValue(m.Id, out var st) ? st : "none";

            void Adiciona(int slot, int? tpId, string? code)
            {
                if (code == null) return;
                if (!isAdmin && (tpId == null || !meusTpIds.Contains(tpId.Value))) return;
                resultado.Add(new
                {
                    matchId = m.Id,
                    slot,
                    tournamentPlayerId = tpId,
                    code,
                    formatted = MatchReportCodeService.Format(code),
                    state = estado,
                });
            }

            Adiciona(1, m.Player1Id, m.Player1ReportCode);
            Adiciona(2, m.Player2Id, m.Player2ReportCode);
        }

        return Ok(resultado);
    }

    [HttpGet("{id}/swiss/status")]
    [AllowAnonymous]
    public async Task<IActionResult> SwissStatus(int id)
    {
        var tournament = await _context.Tournaments.FindAsync(id);
        if (tournament == null) return NotFound();

        await _reportCodes.EnsureCodesAsync(id);
        var estados = await _reportCodes.GetReportStatesAsync(id);

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
            tournament.Mode,
            AllSwissDone       = allSwissDone,
            CurrentRoundDone   = currentRoundDone,
            TopCutGenerated    = topCutGenerated,
            Standings          = standings,
            // ReportState é seguro em endpoint anônimo (só diz "aguardando"/"conflito");
            // os códigos em si NUNCA saem daqui — ver /my-report-codes.
            SwissMatchesByRound = swissMatches
                .GroupBy(m => m.Round)
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.OrderBy(m => m.Id).Select(m => new
                {
                    m.Id, m.Player1Id, m.Player2Id, m.WinnerId,
                    m.IsPlayed, m.IsBye, m.Date,
                    m.Player1GameWins, m.Player2GameWins,
                    ReportState = estados.TryGetValue(m.Id, out var st) ? st : "none",
                }).ToList()),
            TopCutMatches = topCutMatches.Select(m => new
            {
                m.Id, m.MatchType, m.Round,
                m.Player1Id, m.Player2Id, m.WinnerId,
                m.IsPlayed, m.IsBye,
                m.NextMatchId, m.NextMatchPosition,
                m.LoserGoesToMatchId, m.Date,
                m.Player1GameWins, m.Player2GameWins,
                ReportState = estados.TryGetValue(m.Id, out var st2) ? st2 : "none",
            }).ToList(),
        });
    }

    //GET: api/tournament/{id}/participants/{tpId}/deck - decklist de um participante, para a
    //tela de visualização aberta pelo resumo. Público, mas SÓ depois do torneio finalizado:
    //enquanto ele corre, o deck segue privado (o DeckController só libera pro dono ou admin),
    //para ninguém espiar a lista do adversário no meio da competição.
    [HttpGet("{id}/participants/{tpId}/deck")]
    [AllowAnonymous]
    public async Task<IActionResult> GetParticipantDeck(int id, int tpId)
    {
        var tournament = await _context.Tournaments.FindAsync(id);
        if (tournament == null) return NotFound(new { error = "Torneio não encontrado." });
        if (tournament.Status != 2)
            return BadRequest(new { error = "As listas de deck ficam visíveis só depois que o torneio é finalizado." });

        var tp = await _context.TournamentPlayers
            .Include(x => x.Player)
            .FirstOrDefaultAsync(x => x.Id == tpId && x.TournamentId == id);
        if (tp == null) return NotFound(new { error = "Participante não encontrado neste torneio." });
        if (!tp.DeckId.HasValue)
            return NotFound(new { error = "Este participante não registrou a lista do deck." });

        var cards = await DeckViewBuilder.BuildDeckCardsAsync(_context, tp.DeckId.Value);

        return Ok(new
        {
            TournamentId   = id,
            TournamentName = tournament.Name,
            ParticipantId  = tp.Id,
            PlayerName     = tp.DisplayName,
            AvatarUrl      = tp.Player?.AvatarUrl,
            DeckName       = tp.Deck,
            MainDeck       = cards.Where(c => !c.IsDigiEgg),
            DigiEggDeck    = cards.Where(c => c.IsDigiEgg),
        });
    }

    //GET: api/tournament/{id}/recap - resumo pós-torneio (pódio, decks, curiosidades). Só após finalizado.
    [HttpGet("{id}/recap")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRecap(int id)
    {
        var tournament = await _context.Tournaments.FindAsync(id);
        if (tournament == null) return NotFound(new { error = "Torneio não encontrado." });
        if (tournament.Status != 2)
            return BadRequest(new { error = "O resumo só fica disponível depois que o torneio é finalizado." });

        var tps = await _context.TournamentPlayers
            .Where(tp => tp.TournamentId == id)
            .Include(tp => tp.Player)
            .ToListAsync();
        var tpById = tps.ToDictionary(tp => tp.Id);

        // ── Pódio ────────────────────────────────────────────────────────────
        int? championTpId = null, runnerUpTpId = null, thirdTpId = null;
        List<SwissStandingEntry>? fullStandings = null;
        bool isFullyRanked = tournament.Format == 2;

        if (isFullyRanked)
        {
            fullStandings = await _swissService.GetStandingsAsync(id);
            if (fullStandings.Count > 0) championTpId = fullStandings[0].TpId;
            if (fullStandings.Count > 1) runnerUpTpId  = fullStandings[1].TpId;
            if (fullStandings.Count > 2) thirdTpId     = fullStandings[2].TpId;
        }
        else
        {
            (championTpId, runnerUpTpId, thirdTpId) = await TournamentScoringService.GetBracketPlacementsAsync(_context, id);
        }

        var bonusByPosition = new[] { 10, 7, 4 };
        object? PodiumEntry(int position, int? tpId)
        {
            if (!tpId.HasValue || !tpById.TryGetValue(tpId.Value, out var tp)) return null;
            return new
            {
                Position = position,
                ParticipantId = tp.Id,   // usado pelo link da decklist embaixo do pódio
                PlayerId = tp.PlayerId,
                PlayerName = tp.DisplayName,
                AvatarUrl = tp.Player?.AvatarUrl,
                Deck = tp.Deck,
                Bonus = bonusByPosition[position - 1],
                HasDeckList = tp.DeckId.HasValue,
            };
        }
        var podium = new[] { PodiumEntry(1, championTpId), PodiumEntry(2, runnerUpTpId), PodiumEntry(3, thirdTpId) }
            .Where(p => p != null).ToList();

        // ── Decks: capa de cada participante (mesma lógica do DeckController) ──
        var deckIds = tps.Where(tp => tp.DeckId.HasValue).Select(tp => tp.DeckId!.Value).Distinct().ToList();
        var decksById = await _context.Decks.Where(d => deckIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id);
        var deckCards = await _context.DeckCards.Where(dc => deckIds.Contains(dc.DeckId)).ToListAsync();
        var deckCardsByDeck = deckCards.GroupBy(dc => dc.DeckId)
            .ToDictionary(g => g.Key, g => g.OrderBy(dc => dc.IsDigiEgg).ThenBy(dc => dc.CardNumber).ToList());

        var cardNumbers = deckCards.Select(dc => dc.CardNumber)
            .Concat(decksById.Values.Where(d => d.CoverCardNumber != null).Select(d => d.CoverCardNumber!))
            .Distinct().ToList();
        var cardsInfo = await _context.Cards
            .Where(c => cardNumbers.Contains(c.CardNumber))
            .ToDictionaryAsync(c => c.CardNumber);

        string? BuildDeckCoverUrl(int? deckId)
        {
            if (!deckId.HasValue || !decksById.TryGetValue(deckId.Value, out var deck)) return null;
            if (!string.IsNullOrWhiteSpace(deck.CoverCardNumber))
            {
                cardsInfo.TryGetValue(deck.CoverCardNumber, out var coverCard);
                return Card.BuildImageUrl(deck.CoverTcgplayerId ?? coverCard?.TcgplayerId);
            }
            if (deckCardsByDeck.TryGetValue(deck.Id, out var cards) && cards.Count > 0)
            {
                cardsInfo.TryGetValue(cards[0].CardNumber, out var firstCard);
                return Card.BuildImageUrl(cards[0].TcgplayerId ?? firstCard?.TcgplayerId);
            }
            return null;
        }

        var participants = tps
            .OrderByDescending(tp => tp.Id == championTpId)
            .ThenByDescending(tp => tp.Id == runnerUpTpId)
            .ThenByDescending(tp => tp.Id == thirdTpId)
            .ThenBy(tp => tp.DisplayName)
            .Select(tp => new
            {
                tp.Id,
                PlayerId = tp.PlayerId,
                PlayerName = tp.DisplayName,
                AvatarUrl = tp.Player?.AvatarUrl,
                Deck = tp.Deck,
                DeckCoverImageUrl = BuildDeckCoverUrl(tp.DeckId),
                // Só quem registrou um deck salvo tem lista pra abrir na tela de visualização
                HasDeckList = tp.DeckId.HasValue,
            }).ToList();

        // ── Gráfico de pizza: frequência por nome de deck ───────────────────
        var deckNameBreakdown = tps
            .Where(tp => !string.IsNullOrWhiteSpace(tp.Deck))
            .GroupBy(tp => tp.Deck!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new { Deck = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ToList();

        // ── Cartas mais jogadas: só as que aparecem em mais de 1 deck (overlap real).
        // Se nenhuma carta se repete entre decks, mostra 1 carta de destaque (capa) de cada deck. ──
        var sharedCardCounts = deckCards
            .GroupBy(dc => dc.CardNumber)
            .Select(g => new { CardNumber = g.Key, DeckCount = g.Select(x => x.DeckId).Distinct().Count() })
            .Where(g => g.DeckCount > 1)
            .OrderByDescending(g => g.DeckCount)
            .Take(10)
            .ToList();

        string topCardsMode;
        List<object> topCards;

        if (sharedCardCounts.Count > 0)
        {
            topCardsMode = "shared";
            topCards = sharedCardCounts.Select(g =>
            {
                cardsInfo.TryGetValue(g.CardNumber, out var card);
                return (object)new
                {
                    g.CardNumber,
                    Name = card?.Name,
                    ImageUrl = Card.BuildImageUrl(card?.TcgplayerId),
                    g.DeckCount,
                    PlayerName = (string?)null,
                };
            }).ToList();
        }
        else
        {
            topCardsMode = "onePerDeck";
            topCards = tps
                .Where(tp => tp.DeckId.HasValue)
                .Select(tp =>
                {
                    var deckId = tp.DeckId!.Value;
                    string? cardNumber = decksById.TryGetValue(deckId, out var deck) && !string.IsNullOrWhiteSpace(deck.CoverCardNumber)
                        ? deck.CoverCardNumber
                        : (deckCardsByDeck.TryGetValue(deckId, out var cards) && cards.Count > 0 ? cards[0].CardNumber : null);
                    return new { tp, CardNumber = cardNumber };
                })
                .Where(x => x.CardNumber != null)
                .Select(x =>
                {
                    cardsInfo.TryGetValue(x.CardNumber!, out var card);
                    return (object)new
                    {
                        CardNumber = x.CardNumber!,
                        Name = card?.Name,
                        ImageUrl = Card.BuildImageUrl(card?.TcgplayerId),
                        DeckCount = (int?)null,
                        PlayerName = (string?)x.tp.DisplayName,
                    };
                }).ToList();
        }

        // ── Top 3 cartas por quantidade total (soma de cópias entre todos os decks) ──
        var topCardsByQuantity = deckCards
            .GroupBy(dc => dc.CardNumber)
            .Select(g => new { CardNumber = g.Key, TotalQuantity = g.Sum(dc => dc.Quantity) })
            .OrderByDescending(g => g.TotalQuantity)
            .Take(3)
            .Select(g =>
            {
                cardsInfo.TryGetValue(g.CardNumber, out var card);
                return new
                {
                    g.CardNumber,
                    Name = card?.Name,
                    ImageUrl = Card.BuildImageUrl(card?.TcgplayerId),
                    g.TotalQuantity,
                };
            }).ToList();

        // ── Estatísticas gerais ──────────────────────────────────────────────
        var matches = await _context.TournamentMatches.Where(m => m.TournamentId == id).ToListAsync();
        var realMatches = matches.Where(m => m.IsPlayed && !m.IsBye).ToList();
        var totalDraws = realMatches.Count(m => !m.WinnerId.HasValue);
        var lastMatchDate = matches.Where(m => m.Date.HasValue).Select(m => m.Date!.Value).OrderByDescending(d => d).FirstOrDefault();

        return Ok(new
        {
            Tournament = new
            {
                tournament.Id,
                tournament.Name,
                tournament.Format,
                tournament.Mode,
                tournament.StartDate,
                tournament.EndDate,
                FinishedAt = lastMatchDate == default ? (DateTime?)null : lastMatchDate,
                tournament.SwissRounds,
                tournament.TopCutSize,
                tournament.TrophyImageUrl,
            },
            Podium = podium,
            IsFullyRanked = isFullyRanked,
            Standings = isFullyRanked ? fullStandings : null,
            Participants = participants,
            DeckNameBreakdown = deckNameBreakdown,
            TopCards = topCards,
            TopCardsMode = topCardsMode,
            TopCardsByQuantity = topCardsByQuantity,
            Stats = new
            {
                TotalParticipants = tps.Count,
                TotalMatches = realMatches.Count,
                TotalDraws = totalDraws,
                TotalRounds = tournament.Format != 0 ? tournament.SwissRounds : (int?)null,
            },
        });
    }
}
