namespace UNChat.Models
{
    public class UserChat
    {
        int Id { get; set; }
        public string UserId { get; set; }
        public User User { get; set; }

        public string ChatId { get; set; }
        public Chat Chat { get; set; }

        public bool IsAdmin { get; set; } = false; // ✅✅✅✅✅✅✅✅ NOWE POLE
    }

}
