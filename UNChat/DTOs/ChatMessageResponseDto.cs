namespace UNChat.DTOs
{
    public class ChatMessageResponseDto
    {
        public string SenderId { get; set; }
        public string ReceiverId { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
        public List<AttachmentResponseDto> Attachments { get; set; } = new();
    }
}
