using Microsoft.EntityFrameworkCore;
using RankingDigi.Models;
using System.Collections.Generic;
using RankingDigi.Services;

namespace RankingDigi.Data
{
    public class RankingContext : DbContext
    {
        public DbSet<Player> Players { get; set; }
        public DbSet<Match> Matches { get; set; }

        //Sets para os torneios
        public DbSet<Tournament> Tournaments { get; set; }
        public DbSet<TournamentMatch> TournamentMatches { get; set; }
        public RankingContext(DbContextOptions<RankingContext> options) : base(options) { }
        public DbSet<TournamentPlayer> TournamentPlayers { get; set; }
        public DbSet<AppUser> AppUsers { get; set; }
        public DbSet<Season> Seasons { get; set; }
        public DbSet<PlayerSeasonScore> PlayerSeasonScores { get; set; }
        public DbSet<Card> Cards { get; set; }
        public DbSet<CardRestriction> CardRestrictions { get; set; }
        public DbSet<BannedPair> BannedPairs { get; set; }
        public DbSet<Deck> Decks { get; set; }
        public DbSet<DeckCard> DeckCards { get; set; }
        public DbSet<CardArt> CardArts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TournamentMatch>()
                .HasOne(m => m.LoserGoesToMatch)
                .WithMany()
                .HasForeignKey(m => m.LoserGoesToMatchId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<TournamentMatch>()
                .HasOne(m => m.NextMatch)
                .WithMany()
                .HasForeignKey(m => m.NextMatchId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<TournamentPlayer>()
                .HasOne(tp => tp.Tournament)
                .WithMany(t => t.TournamentPlayers)
                .HasForeignKey(tp => tp.TournamentId);

            // PlayerId agora é opcional (guests não têm Player record)
            modelBuilder.Entity<TournamentPlayer>()
                .HasOne(tp => tp.Player)
                .WithMany()
                .HasForeignKey(tp => tp.PlayerId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            // Username único
            modelBuilder.Entity<AppUser>()
                .HasIndex(u => u.Username)
                .IsUnique()
                .HasDatabaseName("IX_AppUsers_Username");

            // AppUser → Player (opcional)
            modelBuilder.Entity<AppUser>()
                .HasOne(u => u.Player)
                .WithMany()
                .HasForeignKey(u => u.PlayerId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            // Índices para acelerar consultas frequentes
            modelBuilder.Entity<Match>()
                .HasIndex(m => m.Player1Id)
                .HasDatabaseName("IX_Matches_Player1Id");

            modelBuilder.Entity<Match>()
                .HasIndex(m => m.Player2Id)
                .HasDatabaseName("IX_Matches_Player2Id");

            modelBuilder.Entity<Match>()
                .HasIndex(m => m.WinnerId)
                .HasDatabaseName("IX_Matches_WinnerId");

            modelBuilder.Entity<Match>()
                .HasIndex(m => m.Date)
                .HasDatabaseName("IX_Matches_Date");

            modelBuilder.Entity<Player>()
                .HasIndex(p => p.Score)
                .HasDatabaseName("IX_Players_Score");

            modelBuilder.Entity<Tournament>()
                .HasIndex(t => t.InviteCode)
                .IsUnique()
                .HasFilter("[InviteCode] IS NOT NULL")
                .HasDatabaseName("IX_Tournaments_InviteCode");

            // Temporadas: snapshot de pontuação final por jogador
            modelBuilder.Entity<PlayerSeasonScore>()
                .HasOne<Season>()
                .WithMany()
                .HasForeignKey(s => s.SeasonId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PlayerSeasonScore>()
                .HasOne<Player>()
                .WithMany()
                .HasForeignKey(s => s.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PlayerSeasonScore>()
                .HasIndex(s => new { s.SeasonId, s.PlayerId })
                .IsUnique()
                .HasDatabaseName("IX_PlayerSeasonScores_SeasonId_PlayerId");

            // Match → Season (opcional; não bloqueia exclusão de temporada)
            modelBuilder.Entity<Match>()
                .HasOne<Season>()
                .WithMany()
                .HasForeignKey(m => m.SeasonId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            // Decks: cartas — número da carta como chave natural
            modelBuilder.Entity<Card>()
                .HasIndex(c => c.CardNumber)
                .IsUnique()
                .HasDatabaseName("IX_Cards_CardNumber");

            modelBuilder.Entity<Deck>()
                .HasOne<Player>()
                .WithMany()
                .HasForeignKey(d => d.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DeckCard>()
                .HasOne<Deck>()
                .WithMany()
                .HasForeignKey(dc => dc.DeckId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DeckCard>()
                .HasOne<Card>()
                .WithMany()
                .HasForeignKey(dc => dc.CardNumber)
                .HasPrincipalKey(c => c.CardNumber)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DeckCard>()
                .HasIndex(dc => new { dc.DeckId, dc.CardNumber, dc.IsDigiEgg })
                .IsUnique()
                .HasDatabaseName("IX_DeckCards_DeckId_CardNumber_IsDigiEgg");

            modelBuilder.Entity<CardRestriction>()
                .HasIndex(r => r.CardNumber)
                .IsUnique()
                .HasDatabaseName("IX_CardRestrictions_CardNumber");

            // Variantes de arte (Alternate Art, Rare Pull, etc.) — mesma carta pras regras,
            // só a imagem muda. CardNumber referencia a mesma chave natural que DeckCard usa.
            modelBuilder.Entity<CardArt>()
                .HasOne<Card>()
                .WithMany()
                .HasForeignKey(a => a.CardNumber)
                .HasPrincipalKey(c => c.CardNumber)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CardArt>()
                .HasIndex(a => new { a.CardNumber, a.TcgplayerId })
                .IsUnique()
                .HasDatabaseName("IX_CardArts_CardNumber_TcgplayerId");

            // Vínculo de deck salvo em partidas e participações de torneio (opcional).
            // Restrict: um deck já usado não pode ser excluído (preserva a decklist para checagem).
            modelBuilder.Entity<Match>()
                .HasOne<Deck>()
                .WithMany()
                .HasForeignKey(m => m.Deck1Id)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Match>()
                .HasOne<Deck>()
                .WithMany()
                .HasForeignKey(m => m.Deck2Id)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TournamentPlayer>()
                .HasOne<Deck>()
                .WithMany()
                .HasForeignKey(tp => tp.DeckId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
