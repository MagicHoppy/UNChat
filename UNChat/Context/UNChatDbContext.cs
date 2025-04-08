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

    }
}
