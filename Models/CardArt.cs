using System.ComponentModel.DataAnnotations.Schema;

namespace RankingDigi.Models
{
    // Variante de arte de uma carta (ex.: Alternate Art, Rare Pull) — mesma carta pra
    // efeito de regras (contagem de cópias, banidas/restritas usam CardNumber), só muda a imagem.
    public class CardArt
    {
        public int Id { get; set; }
        public string CardNumber { get; set; } = string.Empty;
        public int TcgplayerId { get; set; }
        public string Label { get; set; } = "Normal"; // "Normal", "Alternate Art", "Rare Pull", etc.

        [NotMapped]
        public string ImageUrl => Card.BuildImageUrl(TcgplayerId)!;

        [NotMapped]
        public string ImageUrlLarge => Card.BuildImageUrlLarge(TcgplayerId)!;
    }
}
