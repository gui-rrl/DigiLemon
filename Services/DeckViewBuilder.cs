using Microsoft.EntityFrameworkCore;
using RankingDigi.Data;
using RankingDigi.Models;

namespace RankingDigi.Services
{
    /// <summary>
    /// Carta de um deck já pronta para exibição (com a arte escolhida pelo jogador resolvida).
    /// </summary>
    public class DeckCardViewDto
    {
        public string CardNumber { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public bool IsDigiEgg { get; set; }
        public int? TcgplayerId { get; set; }
        public object? Card { get; set; }
    }

    /// <summary>
    /// Monta o conteúdo de um deck para telas de visualização. Compartilhado entre o construtor
    /// de deck (DeckController) e a visualização pública dos decks de um torneio finalizado
    /// (TournamentController), para as duas telas mostrarem exatamente a mesma coisa.
    /// </summary>
    public static class DeckViewBuilder
    {
        public static async Task<List<DeckCardViewDto>> BuildDeckCardsAsync(RankingContext context, int deckId)
        {
            var deckCards = await context.DeckCards.Where(dc => dc.DeckId == deckId).ToListAsync();
            var cardNumbers = deckCards.Select(dc => dc.CardNumber).ToList();
            var cardsInfo = await context.Cards
                .Where(c => cardNumbers.Contains(c.CardNumber))
                .ToDictionaryAsync(c => c.CardNumber);

            return deckCards.Select(dc =>
            {
                cardsInfo.TryGetValue(dc.CardNumber, out var card);
                // Se o jogador escolheu uma arte específica ao adicionar, a imagem reflete essa
                // escolha em vez da arte padrão da carta — o resto dos dados (regras) não muda.
                var effectiveTcgplayerId = dc.TcgplayerId ?? card?.TcgplayerId;
                return new DeckCardViewDto
                {
                    CardNumber  = dc.CardNumber,
                    Quantity    = dc.Quantity,
                    IsDigiEgg   = dc.IsDigiEgg,
                    TcgplayerId = dc.TcgplayerId,
                    Card = card == null ? null : new
                    {
                        card.Id,
                        card.CardNumber,
                        card.Name,
                        card.Type,
                        card.Level,
                        card.PlayCost,
                        card.EvolutionCost,
                        card.EvolutionColor,
                        card.EvolutionLevel,
                        card.Color,
                        card.Color2,
                        card.DigiType,
                        card.Dp,
                        card.Attribute,
                        card.Rarity,
                        card.Stage,
                        card.MainEffect,
                        card.SourceEffect,
                        card.SetName,
                        TcgplayerId   = effectiveTcgplayerId,
                        ImageUrl      = Card.BuildImageUrl(effectiveTcgplayerId),
                        ImageUrlLarge = Card.BuildImageUrlLarge(effectiveTcgplayerId),
                    },
                };
            }).ToList();
        }
    }
}
