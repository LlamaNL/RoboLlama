using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WebhookReceiver.Models;
using WebhookReceiver.Services;

namespace WebhookReceiver.Controllers
{
    [ApiController]
    public class TwitchController : ControllerBase
    {
        private readonly IAlertService _alertService;
        private readonly ISubscriptionService _subscriptionSerivce;

        public TwitchController(ISubscriptionService subscriptionSerivce,
            IAlertService alertService)
        {
            _subscriptionSerivce = subscriptionSerivce;
            _alertService = alertService;
        }

        [HttpGet]
        [Route("/webhooks")]
        public IActionResult Test()
        {
            return Ok("Hello World");
        }

        [HttpPost]
        [Route("/webhooks/callback")]
        public async Task<IActionResult> Get(TwitchEventSubResponse response)
        {
            if (!string.IsNullOrEmpty(response.Challenge))
            {
                (HttpStatusCode code, string body) = await _subscriptionSerivce.ProcessChallenge(response);
                return code == HttpStatusCode.OK ? Ok(body) : BadRequest();
            }

            HttpStatusCode processResponse = await _alertService.ProcessAlert(response.Subscription.Condition.BroadcasterUserId, response.Subscription.Type == "channel.update");
            if (processResponse == HttpStatusCode.OK) return Ok();
            return BadRequest();
        }
    }
}