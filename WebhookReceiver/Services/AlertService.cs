using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WebhookReceiver.Models;
using WebhookReceiver.Models.DTOs;
using WebhookReceiver.Repositories;

namespace WebhookReceiver.Services
{
    public class AlertService : IAlertService
    {
        private readonly IAlertRepository _alertRepository;
        private readonly ITwitchService _twitchService;

        public AlertService(IAlertRepository alertRepository, ITwitchService twitchService)
        {
            _alertRepository = alertRepository;
            _twitchService = twitchService;
        }

        public async Task<HttpStatusCode> ProcessAlert(string id, bool titleChanged)
        {
            ChannelData channel = await _twitchService.GetChannelData(id);
            TwitchStreamAlertDto record = new()
            {
                Announced = titleChanged,
                UserName = channel.BroadcasterName,
                Title = channel.Title,
                ViewerCount = 0,
                GameId = channel.GameId,
                ChannelId = id,
                TitleChanged = titleChanged
            };

            await _alertRepository.Upsert(record);

            return HttpStatusCode.OK;
        }
    }
}