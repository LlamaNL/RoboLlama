using System.Threading.Tasks;
using WebhookReceiver.Models.DTOs;

namespace WebhookReceiver.Repositories
{
    public interface IAlertRepository
    {
        Task Upsert(TwitchStreamAlertDto twitchStreamAlert);
    }
}