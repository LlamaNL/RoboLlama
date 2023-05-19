using System.Threading.Tasks;
using WebhookReceiver.Models.DTOs;

namespace WebhookReceiver.Repositories
{
    public interface ISubscriptionRepository
    {
        Task Delete(string channelId);
        Task Upsert(SubscriptionDto subscription);
    }
}