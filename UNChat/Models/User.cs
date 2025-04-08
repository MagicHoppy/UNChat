using Microsoft.AspNetCore.Identity;

namespace UNChat.Models
{
    public class User : IdentityUser
    {
        public string Name { get; set; }
    }
}
