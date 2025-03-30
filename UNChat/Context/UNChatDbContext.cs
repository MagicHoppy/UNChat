using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using UNChat.Models;

namespace UNChat.Context
{
    // Kontekst bazy danych
 
    public class UNChatDbContext : IdentityDbContext<User>
    {
        public UNChatDbContext(DbContextOptions<UNChatDbContext> options) : base(options) { }
        public DbSet<ChatMessage> ChatMessages { get; set; }

    }
}
