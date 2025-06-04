using Microsoft.Build.Framework;

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

        public List<ChatMessageDelivery> Deliveries { get; set; } = new();
        public List<ChatMessageRead> Reads { get; set; } = new();
    }
    public class ChatMessageDelivery
    {
        public int Id { get; set; }
        public int MessageId { get; set; }
        public ChatMessage Message { get; set; }

        public string UserId { get; set; }
        public DateTime DeliveredAt { get; set; }
    }

    public class ChatMessageRead
    {
        public int Id { get; set; }
        public int MessageId { get; set; }
        public ChatMessage Message { get; set; }

        public string UserId { get; set; }
        public DateTime ReadAt { get; set; }
    }

    public class SendMessageRequest
    {
        /// <summary>User id</summary>
        [Required]
        public string SenderId { get; set; }

        /// <summary>Chat Id</summary>
        [Required]
        public string ChatId { get; set; }

        /// <summary>Message text content (optional)</summary>
        public string? Message { get; set; }

        /// <summary>Attachment (eg. plik JPG, PDF, etc.) (optional)</summary>
        public IFormFile? File { get; set; }
    }

}
