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
        public DbSet<ChatAttachment> ChatAttachments { get; set; }
        public DbSet<Chat> Chats { get; set; }
        public DbSet<UserChat> UserChats { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Chat>()
                .Property(c => c.Id)
                .ValueGeneratedOnAdd(); // Tells EF Core to generate it automatically when a new Chat is added


            modelBuilder.Entity<Friend>()
                .HasKey(f => new { f.Friend1Id, f.Friend2Id });

            modelBuilder.Entity<Friend>()
                .Property(f => f.Status)
                .HasConversion<string>(); // zapis jako string w bazie

            modelBuilder.Entity<UserChat>()
                .HasKey(uc => new { uc.UserId, uc.ChatId });

            modelBuilder.Entity<UserChat>()
                .HasOne(uc => uc.User)
                .WithMany()
                .HasForeignKey(uc => uc.UserId);

            modelBuilder.Entity<UserChat>()
                .HasOne(uc => uc.Chat)
                .WithMany(c => c.Participants)
                .HasForeignKey(uc => uc.ChatId);

        }


    }
}
