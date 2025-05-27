using Lib.Net.Http.WebPush;

namespace UNChat.Models
{
    public class PushSubscriptionWrapper
    {
        public string UserId { get; set; }
        public PushSubscription Subscription { get; set; }
    }
}
