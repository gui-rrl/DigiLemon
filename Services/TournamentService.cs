using Microsoft.EntityFrameworkCore;
using RankingDigi.Data;
using RankingDigi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RankingDigi.Services
{
    public class TournamentService
    {
        private readonly RankingContext _context;

        public TournamentService(RankingContext context)
        {
            _context = context;
        }


        //public async Task GenerateBrackets(int tournamentId)
        //{
        //    // 1. Remover chaveamento existente, se houver
        //    var existingBrackets = await _context.Brackets
        //        .Where(b => b.TournamentId == tournamentId)
        //        .ToListAsync();

        //    if (existingBrackets.Any())
        //    {
        //        // Remove todas as partidas associadas a esses brackets
        //        var bracketIds = existingBrackets.Select(b => b.Id).ToList();
        //        var existingMatches = await _context.TournamentMatches
        //            .Where(m => bracketIds.Contains(m.BracketId))
        //            .ToListAsync();

        //        _context.TournamentMatches.RemoveRange(existingMatches);
        //        _context.Brackets.RemoveRange(existingBrackets);
        //        await _context.SaveChangesAsync();
        //    }

        //    // 2. Obter os IDs dos participantes (decks fixos já em TournamentPlayer)
        //    var participantIds = await _context.TournamentPlayers
        //        .Where(tp => tp.TournamentId == tournamentId)
        //        .Select(tp => tp.PlayerId)
        //        .ToListAsync();

        //    // Validação: quantidade deve ser potência de 2
        //    int totalPlayers = participantIds.Count;
        //    if ((totalPlayers & (totalPlayers - 1)) != 0)
        //        throw new Exception("Número de participantes deve ser potência de 2 (4,8,16...).");

        //    int numberOfRounds = (int)Math.Log2(totalPlayers);

        //    // 3. Embaralhar (ou ordenar) os participantes
        //    var random = new Random();
        //    var shuffled = participantIds.OrderBy(x => random.Next()).ToList();

        //    // 4. Criar os brackets (rodadas)
        //    var brackets = new List<Bracket>();
        //    for (int round = 0; round < numberOfRounds; round++)
        //    {
        //        var bracket = new Bracket
        //        {
        //            TournamentId = tournamentId,
        //            Name = GetRoundName(round, numberOfRounds),
        //            Round = round + 1,
        //            Order = round
        //        };
        //        brackets.Add(bracket);
        //    }
        //    await _context.Brackets.AddRangeAsync(brackets);
        //    await _context.SaveChangesAsync();

        //    // 5. Criar todas as partidas
        //    var allMatches = new List<TournamentMatch>();
        //    for (int round = 0; round < numberOfRounds; round++)
        //    {
        //        var bracket = brackets[round];
        //        int matchesCount = totalPlayers / (int)Math.Pow(2, round + 1);

        //        for (int m = 0; m < matchesCount; m++)
        //        {
        //            var match = new TournamentMatch
        //            {
        //                BracketId = bracket.Id,
        //                IsPlayed = false
        //            };

        //            if (round == 0) // primeira rodada: definir jogadores
        //            {
        //                match.Player1Id = shuffled[m * 2];
        //                match.Player2Id = shuffled[m * 2 + 1];
        //            }
        //            else
        //            {
        //                match.Player1Id = null;
        //                match.Player2Id = null;
        //            }
        //            allMatches.Add(match);
        //        }
        //    }
        //    await _context.TournamentMatches.AddRangeAsync(allMatches);
        //    await _context.SaveChangesAsync();

        //    // 6. Recarregar partidas com brackets para configurar NextMatch
        //    var allMatchesWithBrackets = await _context.TournamentMatches
        //        .Include(m => m.Bracket)
        //        .Where(m => m.Bracket.TournamentId == tournamentId)
        //        .ToListAsync();

        //    var matchesByRound = allMatchesWithBrackets
        //        .GroupBy(m => m.Bracket.Round)
        //        .ToDictionary(g => g.Key, g => g.ToList());

        //    // 7. Configurar relacionamentos (NextMatchId)
        //    for (int round = 1; round <= numberOfRounds; round++)
        //    {
        //        if (!matchesByRound.ContainsKey(round)) continue;
        //        var currentMatches = matchesByRound[round];
        //        if (round < numberOfRounds && matchesByRound.ContainsKey(round + 1))
        //        {
        //            var nextMatches = matchesByRound[round + 1];
        //            for (int i = 0; i < currentMatches.Count; i++)
        //            {
        //                int nextMatchIndex = i / 2;
        //                if (nextMatchIndex < nextMatches.Count)
        //                {
        //                    currentMatches[i].NextMatchId = nextMatches[nextMatchIndex].Id;
        //                    currentMatches[i].NextMatchPosition = (i % 2 == 0) ? 1 : 2;
        //                }
        //            }
        //        }
        //    }

        //    await _context.SaveChangesAsync();
        //}

        // NOVO MÉTODO para dupla eliminação
        public async Task GenerateDoubleElimination(int tournamentId, List<int> playerIds)
        {
            var generator = new DoubleEliminationGenerator(_context);
            await generator.GenerateAsync(tournamentId, playerIds);
        }

        private string GetRoundName(int round, int totalRounds)
        {
            int currentRound = totalRounds - round;
            return currentRound switch
            {
                4 => "Final",
                3 => "Semifinal",
                2 => "Quartas de final",
                1 => "Oitavas de final",
                _ => $"Rodada {currentRound}"
            };
        }
    }
}