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

public class ChatMessageDto
{
    public string SenderId { get; set; }
    public string Message { get; set; }
    public DateTime Timestamp { get; set; }
    public List<ChatAttachmentDto> Attachments { get; set; }
}

public class ChatAttachmentDto
{
    public string FileName { get; set; }
    public string FilePath { get; set; }
}
public class ChatMessageResponseDto
{
    public string SenderId { get; set; }
    public string ReceiverId { get; set; }
    public string Message { get; set; }
    public DateTime Timestamp { get; set; }
    public List<AttachmentResponseDto> Attachments { get; set; } = new();
}

public class AttachmentResponseDto
{
    public string FileName { get; set; }
    public string FileUrl { get; set; }
}

