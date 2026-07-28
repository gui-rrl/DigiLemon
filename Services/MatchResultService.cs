using Microsoft.EntityFrameworkCore;
using RankingDigi.Data;
using RankingDigi.Models;

namespace RankingDigi.Services
{
    public enum MatchResultErrorCode
    {
        None,
        AlreadyPlayed,
        DrawNotAllowed,
        WinnerNotInMatch,
        InvalidGameScore,
    }

    public sealed record MatchResultOutcome(bool Success, MatchResultErrorCode Code, string? Error)
    {
        public static MatchResultOutcome Ok() => new(true, MatchResultErrorCode.None, null);

        public static MatchResultOutcome Fail(MatchResultErrorCode code, string error) =>
            new(false, code, error);
    }

    /// <summary>
    /// Aplica o resultado de uma partida de torneio: placar em games, standings do Swiss,
    /// pontuação no ranking geral, avanço no chaveamento e finalização na Grande Final.
    ///
    /// Fonte única de verdade — usada tanto pelo endpoint do admin
    /// (<c>POST /api/tournamentmatch/{id}/result</c>) quanto pela integração com o DCGO.
    /// Duplicar essa lógica sairia caro: qualquer divergência entre os dois caminhos
    /// corromperia silenciosamente a classificação.
    /// </summary>
    public class MatchResultService
    {
        private readonly RankingContext _context;

        public MatchResultService(RankingContext context)
        {
            _context = context;
        }

        /// <param name="match">Partida já rastreada pelo contexto do chamador.</param>
        /// <remarks>
        /// Chama <c>SaveChangesAsync</c> uma única vez, no fim do ramo executado. Tudo que o
        /// chamador já tiver adicionado ao rastreador (ex.: o MatchReport da integração) entra
        /// no mesmo SaveChanges — é assim que relato e resultado ficam atômicos sem precisar de
        /// transação explícita (que aliás não é possível aqui: EnableRetryOnFailure, ligado no
        /// Program.cs, faz BeginTransactionAsync lançar exceção).
        /// </remarks>
        public async Task<MatchResultOutcome> ApplyAsync(TournamentMatch match, MatchResultDto result)
        {
            if (match.IsPlayed)
                return MatchResultOutcome.Fail(MatchResultErrorCode.AlreadyPlayed, "Esta partida já foi finalizada.");

            bool isDraw = result.WinnerId == 0;
            if (isDraw && match.MatchType != 3)
                return MatchResultOutcome.Fail(MatchResultErrorCode.DrawNotAllowed, "Empate não é permitido em fases eliminatórias — a partida precisa de um vencedor (morte súbita).");
            if (!isDraw && result.WinnerId != match.Player1Id && result.WinnerId != match.Player2Id)
                return MatchResultOutcome.Fail(MatchResultErrorCode.WinnerNotInMatch, "O vencedor precisa ser um dos jogadores da partida (ou 0 para empate, apenas no Swiss).");

            // Placar em games (melhor de 3). Opcional: continua sendo possível registrar só o
            // vencedor, e nesse caso os desempates assumem a convenção 2x0 / 1x1.
            if (result.WinnerGames.HasValue || result.LoserGames.HasValue)
            {
                int wg = result.WinnerGames ?? 0;
                int lg = result.LoserGames ?? 0;
                bool placarValido = isDraw ? (wg == 1 && lg == 1) : (wg == 2 && (lg == 0 || lg == 1));
                if (!placarValido)
                    return MatchResultOutcome.Fail(MatchResultErrorCode.InvalidGameScore, "Placar inválido para melhor de 3: use 2x0, 2x1 ou 1x1 (empate).");

                bool vencedorEhPlayer1 = isDraw ? true : result.WinnerId == match.Player1Id;
                match.Player1GameWins = vencedorEhPlayer1 ? wg : lg;
                match.Player2GameWins = vencedorEhPlayer1 ? lg : wg;
            }

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
                return MatchResultOutcome.Ok();
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
            return MatchResultOutcome.Ok();
        }
    }
}
