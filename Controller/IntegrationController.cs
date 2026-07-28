using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RankingDigi.Data;
using RankingDigi.Models;
using RankingDigi.Services;

namespace RankingDigi.Controller
{
    /// <summary>
    /// API consumida pelo simulador DCGO para reportar resultados de partidas de torneio.
    ///
    /// Modelo: cada partida online tem DOIS códigos, um por jogador. O resultado só é aplicado
    /// quando os dois lados reportam a mesma coisa; divergência vira conflito para o organizador
    /// resolver. É essa dupla confirmação — e não a chave de API — que impede um relato forjado
    /// de mexer na classificação.
    /// </summary>
    [ApiController]
    [Route("api/integration")]
    [AllowAnonymous]                                   // o DCGO não tem contas nem JWT
    [ServiceFilter(typeof(IntegrationKeyFilter))]
    [EnableRateLimiting(IntegrationRateLimit.PolicyName)]
    public class IntegrationController : ControllerBase
    {
        // Serializa o ciclo ler-comparar-aplicar por partida. Os dois clientes do DCGO podem
        // reportar no mesmo milissegundo: sem isso, ambos leriam "o outro já concordou", ambos
        // chamariam ApplyAsync e a pontuação sairia DOBRADA (o guard de IsPlayed é uma checagem
        // sobre o próprio snapshot, não um lock). Vale porque o app roda em processo único —
        // se um dia rodar em várias instâncias, isso vira um token de concorrência no banco.
        private static readonly ConcurrentDictionary<int, SemaphoreSlim> _matchLocks = new();

        private readonly RankingContext _context;
        private readonly MatchResultService _matchResults;
        private readonly MatchReportCodeService _reportCodes;
        private readonly ILogger<IntegrationController> _logger;

        public IntegrationController(
            RankingContext context,
            MatchResultService matchResults,
            MatchReportCodeService reportCodes,
            ILogger<IntegrationController> logger)
        {
            _context = context;
            _matchResults = matchResults;
            _reportCodes = reportCodes;
            _logger = logger;
        }

        [HttpGet("health")]
        public IActionResult Health() => Ok(new
        {
            ok = true,
            server = "RankingDigi",
            apiVersion = 1,
            utc = DateTime.UtcNow,
        });

        /// <summary>Identifica a partida a partir do código e devolve o estado atual.</summary>
        [HttpGet("match/{code}")]
        public async Task<IActionResult> GetMatch(string code)
        {
            var resolvido = await _reportCodes.ResolveAsync(code);
            if (resolvido == null)
            {
                _logger.LogWarning("Integração DCGO: código inexistente {Code} de {Ip}",
                    MatchReportCodeService.Normalize(code), Ip());
                return NotFound(new { error = "Código não encontrado." });
            }

            var (match, slot) = resolvido.Value;
            var tournament = await _context.Tournaments.FindAsync(match.TournamentId);
            var (you, opponent) = await LoadSidesAsync(match, slot);
            var relatos = await LoadReportsAsync(match.Id);

            var bloqueio = Bloqueio(match, tournament);
            var meuRelato = relatos.FirstOrDefault(r => r.PlayerSlot == slot);

            return Ok(new
            {
                matchId = match.Id,
                tournamentId = match.TournamentId,
                tournamentName = tournament?.Name,
                matchType = match.MatchType,
                round = match.Round,
                bestOf = 3,
                yourSlot = slot,
                you = Lado(you),
                opponent = Lado(opponent),
                drawAllowed = match.MatchType == 3,     // empate só existe em rodada Swiss
                canReport = bloqueio == null,
                blockedReason = bloqueio,
                state = Estado(match, relatos),
                opponentReported = relatos.Any(r => r.PlayerSlot != slot),
                yourReport = meuRelato == null ? null : Claim(meuRelato),
                result = match.IsPlayed ? Resultado(match) : null,
            });
        }

        /// <summary>Registra o relato de um dos lados e aplica o resultado se os dois baterem.</summary>
        [HttpPost("match/{code}/report")]
        public async Task<IActionResult> Report(string code, [FromBody] MatchReportDto dto)
        {
            var resolvido = await _reportCodes.ResolveAsync(code);
            if (resolvido == null)
            {
                _logger.LogWarning("Integração DCGO: relato com código inexistente {Code} de {Ip}",
                    MatchReportCodeService.Normalize(code), Ip());
                return NotFound(new { error = "Código não encontrado." });
            }

            var matchId = resolvido.Value.Match.Id;
            var slot = resolvido.Value.Slot;

            var trava = _matchLocks.GetOrAdd(matchId, _ => new SemaphoreSlim(1, 1));
            await trava.WaitAsync();
            try
            {
                // Releitura dentro da trava: outro cliente pode ter aplicado o resultado
                // entre o ResolveAsync acima e a entrada aqui.
                var match = await _context.TournamentMatches.FindAsync(matchId);
                if (match == null) return NotFound(new { error = "Código não encontrado." });

                var tournament = await _context.Tournaments.FindAsync(match.TournamentId);

                if (match.IsPlayed)
                    return Ok(new { state = "resolved", alreadyApplied = true, result = Resultado(match) });

                var bloqueio = Bloqueio(match, tournament);
                if (bloqueio != null)
                    return Conflict(new { error = MensagemBloqueio(bloqueio), reason = bloqueio });

                // ── Normaliza o claim relativo ("eu ganhei") para termos absolutos ──────
                var outcome = (dto?.Outcome ?? string.Empty).Trim().ToLowerInvariant();
                if (outcome != "win" && outcome != "loss" && outcome != "draw")
                    return BadRequest(new { error = "Campo 'outcome' precisa ser 'win', 'loss' ou 'draw'." });

                if (outcome == "draw" && match.MatchType != 3)
                    return BadRequest(new { error = "Empate não é permitido em fases eliminatórias — a partida precisa de um vencedor (morte súbita)." });

                int meus = dto?.YourGameWins ?? (outcome == "win" ? 2 : outcome == "draw" ? 1 : 0);
                int dele = dto?.OpponentGameWins ?? (outcome == "win" ? 0 : outcome == "draw" ? 1 : 2);

                bool placarValido = outcome switch
                {
                    "draw" => meus == 1 && dele == 1,
                    "win"  => meus == 2 && (dele == 0 || dele == 1),
                    _      => dele == 2 && (meus == 0 || meus == 1),
                };
                if (!placarValido)
                    return BadRequest(new { error = "Placar inválido para melhor de 3: use 2x0, 2x1 ou 1x1 (empate)." });

                int meuTpId = (slot == 1 ? match.Player1Id : match.Player2Id)!.Value;
                int adversarioTpId = (slot == 1 ? match.Player2Id : match.Player1Id)!.Value;

                int? vencedorTpId = outcome switch
                {
                    "win"  => meuTpId,
                    "loss" => adversarioTpId,
                    _      => null,
                };
                int p1Games = slot == 1 ? meus : dele;
                int p2Games = slot == 1 ? dele : meus;

                // ── Grava/atualiza o relato deste slot ─────────────────────────────────
                var relatos = await _context.MatchReports
                    .Where(r => r.TournamentMatchId == match.Id)
                    .ToListAsync();

                // Defensivo: se a vaga foi remanejada depois de o código ter sido gerado, os
                // relatos antigos são de outra dupla e não valem mais.
                var obsoletos = relatos.Where(r =>
                    r.ReporterTournamentPlayerId != (r.PlayerSlot == 1 ? match.Player1Id : match.Player2Id)).ToList();
                if (obsoletos.Count > 0)
                {
                    _logger.LogWarning("Integração DCGO: descartando {N} relato(s) obsoleto(s) da partida {MatchId} (slot remanejado)", obsoletos.Count, match.Id);
                    _context.MatchReports.RemoveRange(obsoletos);
                    relatos = relatos.Except(obsoletos).ToList();
                }

                var meu = relatos.FirstOrDefault(r => r.PlayerSlot == slot);
                bool duplicado = false, revisado = false;

                if (meu == null)
                {
                    meu = new MatchReport
                    {
                        TournamentMatchId = match.Id,
                        PlayerSlot = slot,
                        ReporterTournamentPlayerId = meuTpId,
                        ClaimedWinnerTpId = vencedorTpId,
                        ClaimedPlayer1GameWins = p1Games,
                        ClaimedPlayer2GameWins = p2Games,
                        ReporterNickname = Trunca(dto?.ReporterNickname, 64),
                        ClientVersion = Trunca(dto?.ClientVersion, 64),
                        SourceIp = Trunca(Ip(), 64),
                        CreatedAt = DateTime.UtcNow,
                    };
                    _context.MatchReports.Add(meu);
                    relatos.Add(meu);
                }
                else
                {
                    duplicado = meu.ClaimedWinnerTpId == vencedorTpId
                             && meu.ClaimedPlayer1GameWins == p1Games
                             && meu.ClaimedPlayer2GameWins == p2Games;

                    if (!duplicado)
                    {
                        meu.ClaimedWinnerTpId = vencedorTpId;
                        meu.ClaimedPlayer1GameWins = p1Games;
                        meu.ClaimedPlayer2GameWins = p2Games;
                        meu.RevisionCount++;
                        revisado = true;
                    }
                    meu.ReporterNickname = Trunca(dto?.ReporterNickname, 64) ?? meu.ReporterNickname;
                    meu.ClientVersion = Trunca(dto?.ClientVersion, 64) ?? meu.ClientVersion;
                    meu.SourceIp = Trunca(Ip(), 64);
                    meu.UpdatedAt = DateTime.UtcNow;
                }

                _logger.LogInformation(
                    "Integração DCGO: partida {MatchId} slot {Slot} relatou vencedor={Winner} placar={P1}x{P2} nick={Nick} versão={Ver}",
                    match.Id, slot, vencedorTpId?.ToString() ?? "empate", p1Games, p2Games,
                    dto?.ReporterNickname ?? "?", dto?.ClientVersion ?? "?");

                var doAdversario = relatos.FirstOrDefault(r => r.PlayerSlot != slot);

                // ── Só um lado reportou: fica aguardando ───────────────────────────────
                if (doAdversario == null)
                {
                    await _context.SaveChangesAsync();
                    return Accepted(new
                    {
                        state = "awaiting_opponent",
                        duplicate = duplicado,
                        revised = revisado,
                        message = "Resultado registrado. Aguardando a confirmação do adversário.",
                    });
                }

                // ── Os dois reportaram: comparar ───────────────────────────────────────
                bool concordam = doAdversario.ClaimedWinnerTpId == vencedorTpId
                              && doAdversario.ClaimedPlayer1GameWins == p1Games
                              && doAdversario.ClaimedPlayer2GameWins == p2Games;

                if (!concordam)
                {
                    await _context.SaveChangesAsync();
                    _logger.LogWarning("Integração DCGO: CONFLITO na partida {MatchId} — relatos divergentes", match.Id);
                    return Conflict(new
                    {
                        state = "conflict",
                        reason = "reports_disagree",
                        message = "Os relatos divergem. Um organizador vai revisar a partida.",
                        // De propósito NÃO devolvemos o claim do adversário: com ele, quem
                        // quisesse forjar bastaria tentar até casar.
                        yourReport = Claim(meu),
                    });
                }

                // ── Concordam: aplica pelo mesmo caminho do admin ──────────────────────
                var resultado = new MatchResultDto
                {
                    WinnerId = vencedorTpId ?? 0,                       // 0 = empate
                    // Derivado no servidor — nunca vindo do payload do cliente.
                    LoserId = vencedorTpId == null
                        ? null
                        : (vencedorTpId == match.Player1Id ? match.Player2Id : match.Player1Id),
                    WinnerGames = vencedorTpId == null ? 1 : Math.Max(p1Games, p2Games),
                    LoserGames = vencedorTpId == null ? 1 : Math.Min(p1Games, p2Games),
                };

                // O SaveChanges lá dentro grava o relato junto com o resultado — atômico,
                // sem transação explícita (que o EnableRetryOnFailure não permitiria).
                var outcomeAplicacao = await _matchResults.ApplyAsync(match, resultado);
                if (!outcomeAplicacao.Success)
                    return BadRequest(new { error = outcomeAplicacao.Error });

                _logger.LogInformation("Integração DCGO: partida {MatchId} confirmada pelos dois lados e aplicada", match.Id);

                return Ok(new
                {
                    state = "resolved",
                    result = Resultado(match),
                    message = "Resultado confirmado pelos dois jogadores.",
                });
            }
            finally
            {
                trava.Release();
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────────

        private string? Ip() => HttpContext.Connection.RemoteIpAddress?.ToString();

        private static string? Trunca(string? valor, int max) =>
            string.IsNullOrWhiteSpace(valor) ? null
            : valor.Length <= max ? valor.Trim() : valor.Trim()[..max];

        private static string? Bloqueio(TournamentMatch match, Tournament? tournament)
        {
            if (tournament == null) return "tournament_finished";
            // Status 2 = finalizado. Não checar "!= 1" porque Dupla Eliminação fica em 0.
            if (tournament.Status == 2) return "tournament_finished";
            if (tournament.Mode != 1) return "mode_not_online";
            if (match.IsBye) return "match_is_bye";
            if (!match.Player1Id.HasValue || !match.Player2Id.HasValue) return "slot_empty";
            if (match.IsPlayed) return "already_resolved";
            return null;
        }

        private static string MensagemBloqueio(string reason) => reason switch
        {
            "tournament_finished" => "Este torneio já foi finalizado.",
            "mode_not_online"     => "Este torneio não é do modo online.",
            "match_is_bye"        => "Esta partida é um bye e não tem resultado a reportar.",
            "slot_empty"          => "Esta partida ainda não tem os dois jogadores definidos.",
            "already_resolved"    => "Esta partida já foi finalizada.",
            _                     => "Esta partida não aceita relatos no momento.",
        };

        private static string Estado(TournamentMatch match, List<MatchReport> relatos)
        {
            if (match.IsPlayed) return "resolved";
            if (relatos.Count == 0) return "pending";
            if (relatos.Count == 1) return "awaiting_opponent";
            var primeiro = relatos[0];
            bool iguais = relatos.All(r => r.ClaimedWinnerTpId == primeiro.ClaimedWinnerTpId
                                        && r.ClaimedPlayer1GameWins == primeiro.ClaimedPlayer1GameWins
                                        && r.ClaimedPlayer2GameWins == primeiro.ClaimedPlayer2GameWins);
            return iguais ? "awaiting_opponent" : "conflict";
        }

        private async Task<List<MatchReport>> LoadReportsAsync(int matchId) =>
            await _context.MatchReports.Where(r => r.TournamentMatchId == matchId).ToListAsync();

        private async Task<(TournamentPlayer? You, TournamentPlayer? Opponent)> LoadSidesAsync(TournamentMatch match, int slot)
        {
            var ids = new[] { match.Player1Id, match.Player2Id }.Where(i => i.HasValue).Select(i => i!.Value).ToList();
            var jogadores = await _context.TournamentPlayers
                .Include(tp => tp.Player)
                .Where(tp => ids.Contains(tp.Id))
                .ToListAsync();

            var meuId = slot == 1 ? match.Player1Id : match.Player2Id;
            var outroId = slot == 1 ? match.Player2Id : match.Player1Id;
            return (jogadores.FirstOrDefault(tp => tp.Id == meuId),
                    jogadores.FirstOrDefault(tp => tp.Id == outroId));
        }

        private static object? Lado(TournamentPlayer? tp) => tp == null ? null : new
        {
            tournamentPlayerId = tp.Id,
            name = tp.DisplayName,
            deck = tp.Deck,
        };

        private static object Claim(MatchReport r) => new
        {
            winnerTournamentPlayerId = r.ClaimedWinnerTpId,
            isDraw = r.ClaimedWinnerTpId == null,
            player1GameWins = r.ClaimedPlayer1GameWins,
            player2GameWins = r.ClaimedPlayer2GameWins,
            reportedAt = r.UpdatedAt ?? r.CreatedAt,
        };

        private static object Resultado(TournamentMatch m) => new
        {
            winnerTournamentPlayerId = m.WinnerId,
            isDraw = m.WinnerId == null,
            player1GameWins = m.Player1GameWins,
            player2GameWins = m.Player2GameWins,
        };
    }
}
