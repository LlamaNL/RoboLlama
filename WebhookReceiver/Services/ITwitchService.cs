using System.Threading.Tasks;
using WebhookReceiver.Models;

namespace WebhookReceiver.Services
{
    public interface ITwitchService
    {
        Task<ChannelData> GetChannelData(string id);
    }
}