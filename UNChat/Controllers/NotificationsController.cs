using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using UNChat.Models;
using Swashbuckle.AspNetCore.Annotations;

namespace UNChat.Controllers
{
    [Route("notifications")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {
        private static List<PushSubscriptionWrapper> _subscriptions = new();

        [HttpPost("subscribe")]
        [SwaggerOperation(Summary = "Subscribe to push notifications",
                         Description = "Registers a user's device for push notifications")]
        [SwaggerResponse(200, "Subscription successful")]
        public IActionResult Subscribe([FromBody] PushSubscriptionWrapper data)
        {
            _subscriptions.RemoveAll(s =>
                s.UserId == data.UserId ||
                s.Subscription.Endpoint == data.Subscription.Endpoint
            );

            _subscriptions.Add(data);
            return Ok();
        }

        [HttpPost("send")]
        [SwaggerOperation(Summary = "Send push notification",
                         Description = "Sends a push notification to specified users")]
        [SwaggerResponse(200, "Notifications sent successfully")]
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