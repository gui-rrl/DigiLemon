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
        }
    }
}
