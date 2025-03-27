using Microsoft.AspNetCore.Identity;

namespace UNChat.Models
{
    // Model użytkownika
    public class User : IdentityUser
    {
        public string Name { get; set; }
    }
}
