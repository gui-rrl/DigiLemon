using Microsoft.EntityFrameworkCore;
using RankingDigi.Data;
using RankingDigi.Models;

namespace RankingDigi.Services
{
    /// <summary>
    /// Sorteio de deck pros Tournament.DeckMode 1 (Sorteio entre decks próprios) e 2 (Death
    /// Random). Roda uma única vez, no momento em que o torneio inicia — chamado de dentro de
    /// SwissService.StartAsync e TournamentService.GenerateDoubleElimination (nunca direto pelo
    /// controller), pra cobrir também o início automático via TournamentAutoStartService.
    /// </summary>
    public class TournamentDeckDrawService
    {
        private readonly RankingContext _context;

        public TournamentDeckDrawService(RankingContext context)
        {
            _context = context;
        }

        /// <summary>
        /// No-op seguro de chamar sempre: sai na hora se o torneio for DeckMode 0 (normal) ou se
        /// o sorteio já rodou (DeckDrawCompleted) — é isso que protege o caminho do Format 0
        /// (Dupla Eliminação), que não tem transição de Status e pode ter seu chaveamento
        /// regenerado várias vezes pelo admin.
        /// </summary>
        public async Task DrawAsync(int tournamentId)
        {
            var tournament = await _context.Tournaments.FindAsync(tournamentId)
                ?? throw new InvalidOperationException("Torneio não encontrado.");

            if (tournament.DeckMode == 0 || tournament.DeckDrawCompleted)
                return;

            var options = await _context.TournamentPlayerDeckOptions
                .Include(o => o.TournamentPlayer)
                .Where(o => o.TournamentPlayer!.TournamentId == tournamentId)
                .ToListAsync();

            var byPlayer = options
                .GroupBy(o => o.TournamentPlayerId)
                .ToDictionary(g => g.Key, g => g.Select(o => o.DeckId).ToList());

            if (tournament.DeckMode == 1)
                await DrawSelfRandomAsync(byPlayer);
            else
                await DrawDeathRandomAsync(byPlayer, tournamentId);

            _context.TournamentPlayerDeckOptions.RemoveRange(options); // libera os decks não sorteados
            tournament.DeckDrawCompleted = true;
            await _context.SaveChangesAsync();
        }

        // ── DeckMode 1: cada jogador recebe um dos SEUS PRÓPRIOS decks enviados ──────────────
        private async Task DrawSelfRandomAsync(Dictionary<int, List<int>> byPlayer)
        {
            foreach (var (tpId, deckIds) in byPlayer)
            {
                var chosenDeckId = deckIds[Random.Shared.Next(deckIds.Count)];
                var deck = await _context.Decks.FindAsync(chosenDeckId);
                var tp = await _context.TournamentPlayers.FindAsync(tpId);
                tp!.DeckId = chosenDeckId;
                tp.Deck = deck!.Name;
            }
        }

        /// <summary>
        /// DeckMode 2 — Death Random: ninguém pode receber o próprio deck, e cada deck sorteado é
        /// clonado pro destinatário. Algoritmo: embaralha os participantes e forma um ciclo —
        /// participante[i] recebe um deck sorteado ENTRE os enviados por participante[i+1] (índice
        /// circular). Como (i+1) % n nunca é igual a i quando n ≥ 2, a exclusão "não pode receber
        /// o próprio deck" é satisfeita por construção — sem loop de retry nem risco de beco sem
        /// saída (uma tentativa ingênua de "cada um sorteia do pool inteiro evitando o próprio"
        /// pode empacar: com 3 jogadores e 1 deck cada, se A pega o de B e B pega o de A, sobra só
        /// o próprio deck de C pro C). Cada doador é fonte de exatamente 1 destinatário, então
        /// nenhum deck concreto é usado duas vezes — o resto do pool (N×DeckPoolSize − N decks)
        /// fica sem uso, de propósito.
        /// </summary>
        private async Task DrawDeathRandomAsync(Dictionary<int, List<int>> byPlayer, int tournamentId)
        {
            var tpIds = byPlayer.Keys.ToList();
            if (tpIds.Count < 2)
                throw new InvalidOperationException("Death Random exige ao menos 2 participantes com decks enviados.");

            var shuffled = tpIds.OrderBy(_ => Random.Shared.Next()).ToList();
            int n = shuffled.Count;

            for (int i = 0; i < n; i++)
            {
                int recipientTpId = shuffled[i];
                int donorTpId = shuffled[(i + 1) % n];
                var donorDeckIds = byPlayer[donorTpId];
                var sourceDeckId = donorDeckIds[Random.Shared.Next(donorDeckIds.Count)];

                var clone = await CloneDeckAsync(sourceDeckId, recipientTpId, tournamentId);

                var tp = await _context.TournamentPlayers.FindAsync(recipientTpId);
                tp!.DeckId = clone.Id;
                tp.Deck = clone.Name;
            }
        }

        // Mesmo padrão de inserção de DeckController.CreateDeck: grava o Deck primeiro pra gerar
        // o Id, depois as DeckCards copiadas.
        private async Task<Deck> CloneDeckAsync(int sourceDeckId, int recipientTpId, int tournamentId)
        {
            var source = await _context.Decks.FindAsync(sourceDeckId)
                ?? throw new InvalidOperationException("Deck de origem não encontrado.");
            var recipientTp = await _context.TournamentPlayers.FindAsync(recipientTpId)
                ?? throw new InvalidOperationException("Participante não encontrado.");

            var clone = new Deck
            {
                PlayerId = recipientTp.PlayerId!.Value,
                Name = $"{source.Name} (Death Random)",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CoverCardNumber = source.CoverCardNumber,
                CoverTcgplayerId = source.CoverTcgplayerId,
                SourceDeckId = source.Id,
                SourceTournamentId = tournamentId,
            };
            _context.Decks.Add(clone);
            await _context.SaveChangesAsync(); // gera clone.Id antes de gravar as cartas

            var cards = await _context.DeckCards.Where(dc => dc.DeckId == source.Id).ToListAsync();
            _context.DeckCards.AddRange(cards.Select(c => new DeckCard
            {
                DeckId = clone.Id,
                CardNumber = c.CardNumber,
                Quantity = c.Quantity,
                IsDigiEgg = c.IsDigiEgg,
                TcgplayerId = c.TcgplayerId,
            }));
            await _context.SaveChangesAsync();

            return clone;
        }
    }
}
