using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RankingDigi.Data;
using RankingDigi.Models;

namespace RankingDigi.Services
{
    public class DigimonCardApiDto
    {
        public string? Name { get; set; }
        public string? Type { get; set; }
        public string? Id { get; set; }
        public int? Level { get; set; }

        [JsonPropertyName("play_cost")]
        public int? PlayCost { get; set; }

        [JsonPropertyName("evolution_cost")]
        public int? EvolutionCost { get; set; }

        [JsonPropertyName("evolution_color")]
        public string? EvolutionColor { get; set; }

        [JsonPropertyName("evolution_level")]
        public int? EvolutionLevel { get; set; }

        public string? Color { get; set; }
        public string? Color2 { get; set; }

        [JsonPropertyName("digi_type")]
        public string? DigiType { get; set; }

        [JsonPropertyName("digi_type2")]
        public string? DigiType2 { get; set; }

        [JsonPropertyName("digi_type3")]
        public string? DigiType3 { get; set; }

        [JsonPropertyName("digi_type4")]
        public string? DigiType4 { get; set; }

        public int? Dp { get; set; }
        public string? Attribute { get; set; }
        public string? Rarity { get; set; }
        public string? Stage { get; set; }

        [JsonPropertyName("main_effect")]
        public string? MainEffect { get; set; }

        [JsonPropertyName("source_effect")]
        public string? SourceEffect { get; set; }

        [JsonPropertyName("set_name")]
        public List<string>? SetName { get; set; }

        [JsonPropertyName("tcgplayer_id")]
        public int? TcgplayerId { get; set; }
    }

    public class CardSyncService
    {
        private readonly RankingContext _context;
        private readonly HttpClient _httpClient;

        public CardSyncService(RankingContext context, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _httpClient = httpClientFactory.CreateClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (RankingDigi deck sync)");
        }

        // Busca todas as cartas da API pública do digimoncard.io e insere/atualiza na nossa tabela Card.
        // Retorna quantas cartas foram processadas.
        public async Task<int> SyncCardsAsync()
        {
            // Sem o parâmetro "n": a API o ignora nesse modo de busca e retorna a base inteira de uma vez
            // (a lista vem com reimpressões repetidas por set, deduplicadas abaixo pelo número da carta).
            var url = "https://digimoncard.io/api-public/search.php?series=Digimon%20Card%20Game";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();

            var cards = JsonSerializer.Deserialize<List<DigimonCardApiDto>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            }) ?? new List<DigimonCardApiDto>();

            // A API pode repetir a mesma carta uma vez por set em que ela aparece — deduplica por número
            var uniqueByNumber = cards
                .Where(c => !string.IsNullOrWhiteSpace(c.Id))
                .GroupBy(c => c.Id!)
                .Select(g => g.First())
                .ToList();

            var existing = await _context.Cards.ToDictionaryAsync(c => c.CardNumber);

            int processed = 0;
            foreach (var dto in uniqueByNumber)
            {
                var digiTypes = new[] { dto.DigiType, dto.DigiType2, dto.DigiType3, dto.DigiType4 }
                    .Where(t => !string.IsNullOrWhiteSpace(t));
                var digiType = string.Join(" / ", digiTypes);

                if (existing.TryGetValue(dto.Id!, out var card))
                {
                    // já existe — atualiza
                }
                else
                {
                    card = new Card { CardNumber = dto.Id! };
                    _context.Cards.Add(card);
                    existing[dto.Id!] = card;
                }

                card.Name = dto.Name ?? card.Name;
                card.Type = dto.Type ?? card.Type;
                card.Level = dto.Level;
                card.PlayCost = dto.PlayCost;
                card.EvolutionCost = dto.EvolutionCost;
                card.EvolutionColor = dto.EvolutionColor;
                card.EvolutionLevel = dto.EvolutionLevel;
                card.Color = dto.Color ?? card.Color;
                card.Color2 = dto.Color2;
                card.DigiType = string.IsNullOrWhiteSpace(digiType) ? null : digiType;
                card.Dp = dto.Dp;
                card.Attribute = dto.Attribute;
                card.Rarity = dto.Rarity;
                card.Stage = dto.Stage;
                card.MainEffect = dto.MainEffect;
                card.SourceEffect = dto.SourceEffect;
                card.SetName = dto.SetName != null ? string.Join(", ", dto.SetName) : null;
                card.TcgplayerId = dto.TcgplayerId;

                processed++;
            }

            await _context.SaveChangesAsync();
            return processed;
        }
    }
}
