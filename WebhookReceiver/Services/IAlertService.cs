using System.Net;
using System.Threading.Tasks;

namespace WebhookReceiver.Services
{
    public interface IAlertService
    {
        Task<HttpStatusCode> ProcessAlert(string id, bool titleChanged);
    }
}