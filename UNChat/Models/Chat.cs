namespace UNChat.Models
{
    public class Chat
    {
        public string Id { get; set; }
        public string? Name { get; set; } // null dla czatów 1-na-1
        public bool IsGroup { get; set; } // true = grupa, false = prywatny

        public List<UserChat> Participants { get; set; } = new();
        public List<ChatMessage> Messages { get; set; } = new();
    }

}
