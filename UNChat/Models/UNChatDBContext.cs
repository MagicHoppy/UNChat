using Microsoft.EntityFrameworkCore;
using UNChat.Models;

public class UNChatDBContext : DbContext
{
    public UNChatDBContext(DbContextOptions<UNChatDBContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Chat> Chats { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<UserChat> UserChats { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Konfiguracja relacji wiele-do-wielu
        modelBuilder.Entity<UserChat>()
            .HasKey(uc => new { uc.UserId, uc.ChatId });

        modelBuilder.Entity<UserChat>()
            .HasOne(uc => uc.User)
            .WithMany(u => u.UserChats)
            .HasForeignKey(uc => uc.UserId);

        modelBuilder.Entity<UserChat>()
            .HasOne(uc => uc.Chat)
            .WithMany(c => c.UserChats)
            .HasForeignKey(uc => uc.ChatId);
    }
}


