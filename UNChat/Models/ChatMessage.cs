namespace UNChat.Models
{
    public class ChatMessage
    {
        public int Id { get; set; }

        public string ChatId { get; set; }
        public Chat Chat { get; set; }

        public string SenderId { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }

        public List<ChatAttachment> Attachments { get; set; } = new();
        public bool IsPinned { get; set; } = false;

        public List<MessageReaction> Reactions { get; set; } = new();
    }

}
