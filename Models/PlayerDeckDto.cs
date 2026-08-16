public class PlayerDeckDto
{
    public int PlayerId { get; set; }
    public string ?Deck { get; set; }
    public int? DeckId { get; set; }
    public List<int>? DeckIds { get; set; } // usado quando o torneio tem DeckMode != 0 (1-3 decks pro sorteio)
}
