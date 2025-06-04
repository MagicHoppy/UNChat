using Microsoft.AspNetCore.Identity;

namespace UNChat.Models
{
    public class User : IdentityUser
    {
        public string Name { get; set; }
        public bool IsOnline { get; set; }
        public DateTime? LastOnline { get; set; }
        public string? ApiKey { get; set; }

    }
}
