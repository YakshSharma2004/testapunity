
using Microsoft.EntityFrameworkCore;
using testapi1.Domain;
using testapi1.Infrastructure.Persistence;

namespace testapi1.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // ERD Tables
        public DbSet<Player> Players { get; set; }
        public DbSet<Npc> Npcs { get; set; }
        public DbSet<PlayerNpcState> PlayerNpcStates { get; set; }
        public DbSet<ActionCatalog> ActionCatalog { get; set; }
        public DbSet<Interaction> Interactions { get; set; }
        public DbSet<DialogueTemplate> DialogueTemplates { get; set; }
        public DbSet<LoreDoc> LoreDocs { get; set; }
        public DbSet<LoreChunk> LoreChunks { get; set; }
        public DbSet<ProgressionStateAllowedAction> ProgressionStateAllowedActions { get; set; }

        // Progression Session Store
        public DbSet<ProgressionSessionEntity> ProgressionSessions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ── Explicit PKs for entities that don't use EF's default naming convention ──
            modelBuilder.Entity<ActionCatalog>()
                .HasKey(a => a.ActionId);
            modelBuilder.Entity<ActionCatalog>()
                .HasIndex(a => a.Code)
                .IsUnique();
            modelBuilder.Entity<ActionCatalog>()
                .Property(a => a.IsEnabled)
                .HasDefaultValue(true);

            modelBuilder.Entity<DialogueTemplate>()
                .HasKey(d => d.TemplateId);
            modelBuilder.Entity<DialogueTemplate>()
                .HasIndex(d => new { d.NpcId, d.ActionId, d.ToneTag })
                .IsUnique();

            modelBuilder.Entity<LoreDoc>()
                .HasKey(l => l.DocId);
            modelBuilder.Entity<LoreDoc>()
                .HasIndex(l => l.DocKey)
                .IsUnique();
            modelBuilder.Entity<LoreDoc>()
                .HasIndex(l => l.IsActive);
            modelBuilder.Entity<LoreDoc>()
                .Property(l => l.Version)
                .HasDefaultValue(1);
            modelBuilder.Entity<LoreDoc>()
                .Property(l => l.IsActive)
                .HasDefaultValue(true);

            modelBuilder.Entity<LoreChunk>()
                .HasKey(c => c.ChunkId);
            modelBuilder.Entity<LoreChunk>()
                .HasIndex(c => c.ChunkKey)
                .IsUnique();
            modelBuilder.Entity<LoreChunk>()
                .HasIndex(c => new { c.DocId, c.ChunkOrder });
            modelBuilder.Entity<LoreChunk>()
                .HasIndex(c => c.IsActive);
            modelBuilder.Entity<LoreChunk>()
                .Property(c => c.IsActive)
                .HasDefaultValue(true);

            modelBuilder.Entity<ProgressionStateAllowedAction>()
                .HasKey(item => new { item.State, item.ActionId });
            modelBuilder.Entity<ProgressionStateAllowedAction>()
                .Property(item => item.State)
                .HasMaxLength(50);
            modelBuilder.Entity<ProgressionStateAllowedAction>()
                .HasOne(item => item.Action)
                .WithMany(action => action.AllowedInStates)
                .HasForeignKey(item => item.ActionId);

            // ── PLAYER_NPC_STATE — composite PK ──
            modelBuilder.Entity<PlayerNpcState>()
                .HasKey(p => new { p.PlayerId, p.NpcId });

            modelBuilder.Entity<PlayerNpcState>()
                .HasOne(p => p.Player)
                .WithMany(p => p.NpcStates)
                .HasForeignKey(p => p.PlayerId);

            modelBuilder.Entity<PlayerNpcState>()
                .HasOne(p => p.Npc)
                .WithMany(n => n.PlayerStates)
                .HasForeignKey(p => p.NpcId);

            // ── PLAYER_NPC_STATE — decimal precision (0..1) ──
            modelBuilder.Entity<PlayerNpcState>()
                .Property(p => p.Trust).HasPrecision(4, 2);
            modelBuilder.Entity<PlayerNpcState>()
                .Property(p => p.Patience).HasPrecision(4, 2);
            modelBuilder.Entity<PlayerNpcState>()
                .Property(p => p.Curiosity).HasPrecision(4, 2);
            modelBuilder.Entity<PlayerNpcState>()
                .Property(p => p.Openness).HasPrecision(4, 2);

            // ── NPC — decimal precision for base traits ──
            modelBuilder.Entity<Npc>()
                .Property(n => n.BaseFriendliness).HasPrecision(4, 2);
            modelBuilder.Entity<Npc>()
                .Property(n => n.BasePatience).HasPrecision(4, 2);
            modelBuilder.Entity<Npc>()
                .Property(n => n.BaseCuriosity).HasPrecision(4, 2);
            modelBuilder.Entity<Npc>()
                .Property(n => n.BaseOpenness).HasPrecision(4, 2);
            modelBuilder.Entity<Npc>()
                .Property(n => n.BaseConfidence).HasPrecision(4, 2);
            modelBuilder.Entity<Npc>()
                .HasIndex(n => n.NpcCode)
                .IsUnique();

            // ── INTERACTION — relationships ──
            modelBuilder.Entity<Interaction>()
                .HasOne(i => i.Player)
                .WithMany(p => p.Interactions)
                .HasForeignKey(i => i.PlayerId);

            modelBuilder.Entity<Interaction>()
                .HasOne(i => i.Npc)
                .WithMany(n => n.Interactions)
                .HasForeignKey(i => i.NpcId);

            modelBuilder.Entity<Interaction>()
                .HasOne(i => i.ChosenAction)
                .WithMany(a => a.Interactions)
                .HasForeignKey(i => i.ChosenActionId)
                .IsRequired(false);

            modelBuilder.Entity<Interaction>()
                .Property(i => i.SessionId)
                .HasMaxLength(100);

            modelBuilder.Entity<Interaction>()
                .HasIndex(i => new { i.SessionId, i.OccurredAt });

            // ── INTERACTION — decimal precision ──
            modelBuilder.Entity<Interaction>()
                .Property(i => i.Sentiment).HasPrecision(4, 2);
            modelBuilder.Entity<Interaction>()
                .Property(i => i.Friendliness).HasPrecision(4, 2);
            modelBuilder.Entity<Interaction>()
                .Property(i => i.RewardScore).HasPrecision(6, 4);

            // ── DIALOGUE_TEMPLATE — relationships ──
            modelBuilder.Entity<DialogueTemplate>()
                .HasOne(d => d.Npc)
                .WithMany(n => n.DialogueTemplates)
                .HasForeignKey(d => d.NpcId);

            modelBuilder.Entity<DialogueTemplate>()
                .HasOne(d => d.Action)
                .WithMany(a => a.DialogueTemplates)
                .HasForeignKey(d => d.ActionId);

            // ── LORE_DOC — nullable NPC FK ──
            modelBuilder.Entity<LoreDoc>()
                .HasOne(l => l.Npc)
                .WithMany(n => n.LoreDocs)
                .HasForeignKey(l => l.NpcId)
                .IsRequired(false);

            // ── LORE_CHUNK ──
            modelBuilder.Entity<LoreChunk>()
                .HasOne(c => c.Doc)
                .WithMany(d => d.Chunks)
                .HasForeignKey(c => c.DocId);

            // ── PROGRESSION_SESSIONS ──
            modelBuilder.Entity<ProgressionSessionEntity>(entity =>
            {
                entity.HasKey(e => e.SessionId);
                entity.Property(e => e.SessionId).HasMaxLength(100);
                entity.Property(e => e.State).HasMaxLength(50);
                entity.Property(e => e.Ending).HasMaxLength(50);
                entity.Property(e => e.ComposureState).HasMaxLength(50);
                entity.Property(e => e.ProofTier).HasMaxLength(50);
                entity.HasIndex(e => e.ExpiresAtUtc);
                entity.HasIndex(e => new { e.PlayerId, e.UpdatedAtUtc });
                entity.HasOne<Player>()
                    .WithMany()
                    .HasForeignKey(e => e.PlayerId);
            });
        }
    }
}
