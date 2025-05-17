namespace UNChat.DTOs
{
    public class ChatMessageDto
    {
        public int Id { get; set; }
        public string SenderId { get; set; }
        public string SenderName { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
        public List<ChatAttachmentDto> Attachments { get; set; }
    }
}
