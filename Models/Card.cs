using System.ComponentModel.DataAnnotations.Schema;

namespace RankingDigi.Models
{
    // Dados de uma carta, sincronizados a partir da API pública do digimoncard.io
    public class Card
    {
        public int Id { get; set; }
        public string CardNumber { get; set; } = string.Empty; // ex.: "BT5-109", "P-021" — único
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // Digimon, Tamer, Option, Digi-Egg
        public int? Level { get; set; }
        public int? PlayCost { get; set; }
        public int? EvolutionCost { get; set; }
        public string? EvolutionColor { get; set; }
        public int? EvolutionLevel { get; set; }
        public string Color { get; set; } = string.Empty;
        public string? Color2 { get; set; }
        public string? DigiType { get; set; }
        public int? Dp { get; set; }
        public string? Attribute { get; set; }
        public string? Rarity { get; set; }
        public string? Stage { get; set; }
        public string? MainEffect { get; set; }
        public string? SourceEffect { get; set; }
        public string? SetName { get; set; }
        public int? TcgplayerId { get; set; } // id do produto na TCGplayer — usado só para montar a URL da imagem

        // Imagem da carta via CDN público da TCGplayer (hotlink, não hospedamos a arte).
        // Nem toda carta tem tcgplayer_id (ex.: promos raras) — nesses casos fica null e o front usa um placeholder.
        [NotMapped]
        public string? ImageUrl => BuildImageUrl(TcgplayerId);

        // Versão em alta resolução, usada só no preview ampliado ao passar o mouse.
        [NotMapped]
        public string? ImageUrlLarge => BuildImageUrlLarge(TcgplayerId);

        public static string? BuildImageUrl(int? tcgplayerId) => tcgplayerId.HasValue
            ? $"https://product-images.tcgplayer.com/fit-in/437x437/{tcgplayerId}.jpg"
            : null;

        public static string? BuildImageUrlLarge(int? tcgplayerId) => tcgplayerId.HasValue
            ? $"https://tcgplayer-cdn.tcgplayer.com/product/{tcgplayerId}_in_1000x1000.jpg"
            : null;
    }
}
