using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RankingDigi.Data;
using RankingDigi.Models;
using System.Security.Claims;

namespace RankingDigi.Controller
{
    public class DeckCardInputDto
    {
        public string CardNumber { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public bool IsDigiEgg { get; set; }
        public int? TcgplayerId { get; set; } // arte escolhida (null = arte padrão da carta)
    }

    public class SaveDeckDto
    {
        public int PlayerId { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<DeckCardInputDto> Cards { get; set; } = new();
        public string? CoverCardNumber { get; set; }
        public int? CoverTcgplayerId { get; set; }
    }

    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DeckController : ControllerBase
    {
        private readonly RankingContext _context;

        public DeckController(RankingContext context)
        {
            _context = context;
        }

        private async Task<bool> CanManageDeckForPlayerAsync(int playerId)
        {
            if (User.IsInRole("Admin")) return true;
            var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var appUser = await _context.AppUsers.FindAsync(currentUserId);
            return appUser?.PlayerId == playerId;
        }

        private async Task<bool> IsDeckLockedAsync(int deckId)
        {
            bool inMatch = await _context.Matches.AnyAsync(m => m.Deck1Id == deckId || m.Deck2Id == deckId);
            if (inMatch) return true;
            return await _context.TournamentPlayers.AnyAsync(tp => tp.DeckId == deckId);
        }

        // Valida as regras oficiais do Digimon Card Game: 50 cartas no deck principal,
        // até 5 no deck de Digi-Egg, máximo 4 cópias (ou o limite da lista de restrições),
        // cartas banidas e pares banidos.
        private async Task<List<string>> ValidateDeckAsync(List<DeckCardInputDto> cards)
        {
            var errors = new List<string>();
            cards = cards.Where(c => c.Quantity > 0).ToList();

            int mainTotal = cards.Where(c => !c.IsDigiEgg).Sum(c => c.Quantity);
            int digiEggTotal = cards.Where(c => c.IsDigiEgg).Sum(c => c.Quantity);

            if (mainTotal != 50)
                errors.Add($"O deck principal precisa ter exatamente 50 cartas (atual: {mainTotal}).");
            if (digiEggTotal > 5)
                errors.Add($"O deck de Digi-Egg pode ter no máximo 5 cartas (atual: {digiEggTotal}).");

            var cardNumbers = cards.Select(c => c.CardNumber).Distinct().ToList();
            var knownCards = await _context.Cards
                .Where(c => cardNumbers.Contains(c.CardNumber))
                .Select(c => c.CardNumber)
                .ToListAsync();
            var unknown = cardNumbers.Except(knownCards).ToList();
            if (unknown.Count > 0)
                errors.Add($"Carta(s) não encontrada(s) na base: {string.Join(", ", unknown)}.");

            var restrictions = await _context.CardRestrictions
                .Where(r => cardNumbers.Contains(r.CardNumber))
                .ToDictionaryAsync(r => r.CardNumber, r => r.MaxCopies);

            foreach (var group in cards.GroupBy(c => c.CardNumber))
            {
                int qty = group.Sum(c => c.Quantity);
                if (restrictions.TryGetValue(group.Key, out var maxAllowed))
                {
                    if (maxAllowed == 0)
                        errors.Add($"{group.Key} está banida e não pode ser incluída.");
                    else if (qty > maxAllowed)
                        errors.Add($"{group.Key} está restrita a {maxAllowed} cópia(s) (atual: {qty}).");
                }
                else if (qty > 4)
                {
                    errors.Add($"{group.Key}: máximo de 4 cópias (atual: {qty}).");
                }
            }

            var cardNumberSet = cardNumbers.ToHashSet();
            var pairs = await _context.BannedPairs.ToListAsync();
            foreach (var pair in pairs)
            {
                if (cardNumberSet.Contains(pair.CardNumberA) && cardNumberSet.Contains(pair.CardNumberB))
                    errors.Add($"{pair.CardNumberA} e {pair.CardNumberB} não podem estar no mesmo deck (par banido).");
            }

            return errors;
        }

        // A capa só é aceita se apontar pra uma carta que realmente está no deck sendo salvo —
        // evita ficar com uma referência solta se o jogador remover a carta depois de escolher a capa.
        private static (string? CoverCardNumber, int? CoverTcgplayerId) ResolveCover(SaveDeckDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.CoverCardNumber)) return (null, null);
            var isInDeck = dto.Cards.Any(c => c.Quantity > 0 && c.CardNumber == dto.CoverCardNumber);
            return isInDeck ? (dto.CoverCardNumber, dto.CoverTcgplayerId) : (null, null);
        }

        [HttpGet]
        public async Task<IActionResult> GetDecks([FromQuery] int playerId)
        {
            if (playerId == 0)
                return BadRequest(new { error = "Informe o playerId." });

            var decks = await _context.Decks
                .Where(d => d.PlayerId == playerId)
                .OrderByDescending(d => d.UpdatedAt)
                .ToListAsync();

            var deckIds = decks.Select(d => d.Id).ToList();
            var cardCounts = await _context.DeckCards
                .Where(dc => deckIds.Contains(dc.DeckId))
                .GroupBy(dc => dc.DeckId)
                .Select(g => new { DeckId = g.Key, Total = g.Sum(x => x.Quantity) })
                .ToDictionaryAsync(x => x.DeckId, x => x.Total);

            var lockedIds = (await _context.Matches
                .Where(m => (m.Deck1Id != null && deckIds.Contains(m.Deck1Id.Value)) || (m.Deck2Id != null && deckIds.Contains(m.Deck2Id.Value)))
                .Select(m => new { m.Deck1Id, m.Deck2Id })
                .ToListAsync())
                .SelectMany(m => new[] { m.Deck1Id, m.Deck2Id })
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Union(await _context.TournamentPlayers
                    .Where(tp => tp.DeckId != null && deckIds.Contains(tp.DeckId.Value))
                    .Select(tp => tp.DeckId!.Value)
                    .ToListAsync())
                .ToHashSet();

            // Imagem de capa: a escolhida pelo jogador, ou (se nenhuma) a primeira carta do deck
            // principal — assim todo deck aparece com uma capa, mesmo os salvos antes dessa opção existir.
            var allDeckCards = await _context.DeckCards
                .Where(dc => deckIds.Contains(dc.DeckId))
                .ToListAsync();

            var referencedCardNumbers = allDeckCards.Select(dc => dc.CardNumber)
                .Concat(decks.Where(d => d.CoverCardNumber != null).Select(d => d.CoverCardNumber!))
                .Distinct()
                .ToList();
            var cardsInfo = await _context.Cards
                .Where(c => referencedCardNumbers.Contains(c.CardNumber))
                .ToDictionaryAsync(c => c.CardNumber);

            var deckCardsByDeck = allDeckCards
                .GroupBy(dc => dc.DeckId)
                .ToDictionary(g => g.Key, g => g.OrderBy(dc => dc.IsDigiEgg).ThenBy(dc => dc.CardNumber).ToList());

            string? BuildCoverImageUrl(Deck d)
            {
                if (!string.IsNullOrWhiteSpace(d.CoverCardNumber))
                {
                    cardsInfo.TryGetValue(d.CoverCardNumber, out var coverCard);
                    return Card.BuildImageUrl(d.CoverTcgplayerId ?? coverCard?.TcgplayerId);
                }
                if (deckCardsByDeck.TryGetValue(d.Id, out var cards) && cards.Count > 0)
                {
                    cardsInfo.TryGetValue(cards[0].CardNumber, out var firstCard);
                    return Card.BuildImageUrl(cards[0].TcgplayerId ?? firstCard?.TcgplayerId);
                }
                return null;
            }

            var result = decks.Select(d => new
            {
                d.Id,
                d.Name,
                d.CreatedAt,
                d.UpdatedAt,
                CardCount = cardCounts.TryGetValue(d.Id, out var c) ? c : 0,
                IsLocked = lockedIds.Contains(d.Id),
                CoverImageUrl = BuildCoverImageUrl(d),
            });

            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetDeck(int id)
        {
            var deck = await _context.Decks.FindAsync(id);
            if (deck == null) return NotFound();
            if (!await CanManageDeckForPlayerAsync(deck.PlayerId)) return Forbid();

            var deckCards = await _context.DeckCards.Where(dc => dc.DeckId == id).ToListAsync();
            var cardNumbers = deckCards.Select(dc => dc.CardNumber).ToList();
            var cardsInfo = await _context.Cards
                .Where(c => cardNumbers.Contains(c.CardNumber))
                .ToDictionaryAsync(c => c.CardNumber);

            var shaped = deckCards.Select(dc =>
            {
                cardsInfo.TryGetValue(dc.CardNumber, out var card);
                // Se o jogador escolheu uma arte específica ao adicionar, a imagem reflete essa
                // escolha em vez da arte padrão da carta — o resto dos dados (regras) não muda.
                var effectiveTcgplayerId = dc.TcgplayerId ?? card?.TcgplayerId;
                return new
                {
                    dc.CardNumber,
                    dc.Quantity,
                    dc.IsDigiEgg,
                    dc.TcgplayerId,
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
                        TcgplayerId = effectiveTcgplayerId,
                        ImageUrl = Card.BuildImageUrl(effectiveTcgplayerId),
                        ImageUrlLarge = Card.BuildImageUrlLarge(effectiveTcgplayerId),
                    },
                };
            });

            return Ok(new
            {
                deck.Id,
                deck.PlayerId,
                deck.Name,
                deck.CreatedAt,
                deck.UpdatedAt,
                deck.CoverCardNumber,
                deck.CoverTcgplayerId,
                IsLocked = await IsDeckLockedAsync(id),
                MainDeck = shaped.Where(c => !c.IsDigiEgg),
                DigiEggDeck = shaped.Where(c => c.IsDigiEgg),
            });
        }

        [HttpPost]
        public async Task<IActionResult> CreateDeck(SaveDeckDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { error = "Informe o nome do deck." });
            if (!await CanManageDeckForPlayerAsync(dto.PlayerId))
                return Forbid();

            var errors = await ValidateDeckAsync(dto.Cards);
            if (errors.Count > 0)
                return BadRequest(new { error = "Deck inválido.", errors });

            var (coverCardNumber, coverTcgplayerId) = ResolveCover(dto);
            var deck = new Deck
            {
                PlayerId = dto.PlayerId,
                Name = dto.Name.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CoverCardNumber = coverCardNumber,
                CoverTcgplayerId = coverTcgplayerId,
            };
            _context.Decks.Add(deck);
            await _context.SaveChangesAsync();

            foreach (var c in dto.Cards.Where(c => c.Quantity > 0))
            {
                _context.DeckCards.Add(new DeckCard
                {
                    DeckId = deck.Id,
                    CardNumber = c.CardNumber,
                    Quantity = c.Quantity,
                    IsDigiEgg = c.IsDigiEgg,
                    TcgplayerId = c.TcgplayerId,
                });
            }
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetDeck), new { id = deck.Id }, new { deck.Id });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateDeck(int id, SaveDeckDto dto)
        {
            var deck = await _context.Decks.FindAsync(id);
            if (deck == null) return NotFound();
            if (!await CanManageDeckForPlayerAsync(deck.PlayerId)) return Forbid();

            if (await IsDeckLockedAsync(id))
                return Conflict(new { error = "Este deck já foi usado em uma partida ou torneio e não pode mais ser editado. Crie um novo deck para fazer alterações." });

            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { error = "Informe o nome do deck." });

            var errors = await ValidateDeckAsync(dto.Cards);
            if (errors.Count > 0)
                return BadRequest(new { error = "Deck inválido.", errors });

            var (coverCardNumber, coverTcgplayerId) = ResolveCover(dto);
            deck.Name = dto.Name.Trim();
            deck.UpdatedAt = DateTime.UtcNow;
            deck.CoverCardNumber = coverCardNumber;
            deck.CoverTcgplayerId = coverTcgplayerId;

            var existingCards = await _context.DeckCards.Where(dc => dc.DeckId == id).ToListAsync();
            _context.DeckCards.RemoveRange(existingCards);
            foreach (var c in dto.Cards.Where(c => c.Quantity > 0))
            {
                _context.DeckCards.Add(new DeckCard
                {
                    DeckId = id,
                    CardNumber = c.CardNumber,
                    Quantity = c.Quantity,
                    IsDigiEgg = c.IsDigiEgg,
                    TcgplayerId = c.TcgplayerId,
                });
            }
            await _context.SaveChangesAsync();

            return Ok(new { deck.Id });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDeck(int id)
        {
            var deck = await _context.Decks.FindAsync(id);
            if (deck == null) return NotFound();
            if (!await CanManageDeckForPlayerAsync(deck.PlayerId)) return Forbid();

            if (await IsDeckLockedAsync(id))
                return Conflict(new { error = "Este deck já foi usado em uma partida ou torneio e não pode ser excluído." });

            var existingCards = await _context.DeckCards.Where(dc => dc.DeckId == id).ToListAsync();
            _context.DeckCards.RemoveRange(existingCards);
            _context.Decks.Remove(deck);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
