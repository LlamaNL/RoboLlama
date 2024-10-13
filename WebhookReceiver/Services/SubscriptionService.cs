using System;
using System.Net;
using System.Threading.Tasks;
using WebhookReceiver.Models;
using WebhookReceiver.Models.DTOs;
using WebhookReceiver.Repositories;

namespace WebhookReceiver.Services
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly ISubscriptionRepository _subscriptionRepository;

        public SubscriptionService(ISubscriptionRepository subscriptionRepository)
        {
            _subscriptionRepository = subscriptionRepository;
        }

        public async Task<(HttpStatusCode code, string body)> ProcessChallenge(TwitchEventSubResponse response)
        {
            if (string.IsNullOrEmpty(response.Challenge)) return (HttpStatusCode.BadRequest, null);

            SubscriptionDto subscription = new()
            {
                ChannelId = response.Subscription.Condition.BroadcasterUserId,
                RegistryDate = DateTime.Now,                
            };

			if (response.Subscription.Type == "channel.update")
			{
                subscription.UpdateId = response.Subscription.Id;
			}
            else
            {
                subscription.SubscriptionId = response.Subscription.Id;
			}

			await _subscriptionRepository.Upsert(subscription);
            return (HttpStatusCode.OK, response.Challenge);
        }
    }
}