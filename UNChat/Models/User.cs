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
        public string PasswordHash { get; set; } // Przechowywanie hasła w postaci hasha

        public ICollection<Message> MessagesSent { get; set; }
        public ICollection<Message> MessagesReceived { get; set; }
    }
}