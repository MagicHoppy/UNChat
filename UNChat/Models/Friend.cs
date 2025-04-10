namespace UNChat.Models
{
    public enum FriendStatus
    {
        Pending,
        Accepted
    }

    public class Friend
    {
        public string Friend1Id { get; set; } // wysyłający zaproszenie
        public string Friend2Id { get; set; } // odbiorca zaproszenia
        public FriendStatus Status { get; set; }
    }
}
