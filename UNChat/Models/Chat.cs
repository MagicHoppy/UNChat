using UNChat.Models;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace UNChat.Models
{
    public class Chat
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; }

        public bool IsPrivate { get; set; } = true; // Domyślnie prywatna rozmowa

        // Relacja wiele-do-wielu z User
        public ICollection<UserChat> UserChats { get; set; } = new List<UserChat>();

        public ICollection<Message> Messages { get; set; } = new List<Message>();
    }


}


