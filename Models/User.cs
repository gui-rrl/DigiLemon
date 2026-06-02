using Microsoft.AspNetCore.Identity;

namespace RankingDigi.Models
{
    public class User : IdentityUser
    {
        // Chave estrangeira para o jogador (Player)
        public int? PlayerId { get; set; }
        public Player Player { get; set; }
    }
}
