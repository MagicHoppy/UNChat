using UNChat.Models;

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace UNChat.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Username { get; set; }

        [Required]
        public string PasswordHash { get; set; }

        public ICollection<Message> MessagesSent { get; set; }

        public ICollection<UserChat> UserChats { get; set; } = new List<UserChat>();
    }

}