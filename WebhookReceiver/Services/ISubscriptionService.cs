using System.Net;
using System.Threading.Tasks;
using WebhookReceiver.Models;

namespace WebhookReceiver.Services
{
    public interface ISubscriptionService
    {
        Task<(HttpStatusCode code, string body)> ProcessChallenge(TwitchEventSubResponse response);
    }
}