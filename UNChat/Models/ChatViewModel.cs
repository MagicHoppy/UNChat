namespace UNChat.Models
{
    public class ChatViewModel
    {
        public string UserId { get; set; }
        public List<string> Emojis { get; set; } = new List<string>
    {
        "😀", "😂", "😍", "😎", "😢", "🤔", "🎉", "❤️", "👍", "👏"
    };

    }
}
