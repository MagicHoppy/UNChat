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

        public ICollection<User> Users { get; set; }
        public ICollection<Message> Messages { get; set; }
    }
}


