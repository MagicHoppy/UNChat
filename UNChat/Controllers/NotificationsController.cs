using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using UNChat.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UNChat.Controllers
{
    [Route("notifications")]
    public class NotificationsController : Controller
    {
        // Updated to store userId + subscription
        private static List<PushSubscriptionWrapper> _subscriptions = new();

        // POST: /notifications/subscribe
        [HttpPost("subscribe")]
        public IActionResult Subscribe([FromBody] PushSubscriptionWrapper data)
        {
            // Optional cleanup: remove old entries
            _subscriptions.RemoveAll(s =>
                s.UserId == data.UserId ||
                s.Subscription.Endpoint == data.Subscription.Endpoint
            );

            _subscriptions.Add(data);
            return Ok();
        }

        // POST: /notifications/send
        [HttpPost("send")]
        public async Task<IActionResult> SendNotification([FromBody] dynamic body)
        {
            string message = body.message;
            List<string> userIds = ((IEnumerable<dynamic>)body.userIds).Select(u => (string)u).ToList();

            var vapidDetails = new VapidAuthentication(
                "BKY37T-xh1GCXAWSBySR27YKyV0MxZpODbVIRXH4CkbkScQOhb9mMKyRcS24R3P8T1yjCRXXo8DAPZ8EilT7ZGM",
                "U3PiqmfgvhwvTgeQOmuv8-LoUkWsoyeGRwbr_8Jm9Yo"
            )
            {
                Subject = "mailto:you@example.com"
            };

            var client = new PushServiceClient { DefaultAuthentication = vapidDetails };

            foreach (var sub in _subscriptions.Where(s => userIds.Contains(s.UserId)))
            {
                var payload = JsonConvert.SerializeObject(new
                {
                    title = "Nowa wzmianka!",
                    body = message
                });

                var pushMessage = new PushMessage(payload)
                {
                    Urgency = PushMessageUrgency.High,
                    Topic = "mention"
                };

                await client.RequestPushMessageDeliveryAsync(sub.Subscription, pushMessage);
            }

            return Ok("Push sent to mentioned users.");
        }
    }
}
