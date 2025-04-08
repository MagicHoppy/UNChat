using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using UNChat.Models;

namespace UNChat.Context
{
    public class UNChatDbContext : IdentityDbContext<User>
    {
        public UNChatDbContext(DbContextOptions<UNChatDbContext> options) : base(options) { Database.EnsureCreated(); }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<Emoji> Emojis { get; set; }
        public DbSet<Friend> Friends { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Composite key
            modelBuilder.Entity<Friend>()
                .HasKey(f => new { f.Friend1Id, f.Friend2Id });
        }

    }
}
