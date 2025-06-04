namespace UNChat.DTOs
{
    public class EditMessageDto
    {
        /// <summary>
        /// Id of the message being edited.
        /// </summary>
        public int MessageId { get; set; }
        /// <summary>
        /// The new message content.
        /// </summary>
        public string NewMessage { get; set; }
    }

}
