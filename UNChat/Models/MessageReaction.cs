namespace UNChat.Models
{
    public class MessageReaction
    {
        public int Id { get; set; }

        public int ChatMessageId { get; set; }
        public ChatMessage ChatMessage { get; set; }

        public string UserId { get; set; }
        public User User { get; set; }

        public int EmojiId { get; set; }
        public Emoji Emoji { get; set; }

    }
}
