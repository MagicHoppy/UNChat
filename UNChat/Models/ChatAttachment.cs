using UNChat.Models;

namespace UNChat.Models
{
    public class ChatAttachment
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public int ChatMessageId { get; set; }

        public ChatMessage ChatMessage { get; set; }
    }
}

