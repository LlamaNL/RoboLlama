using System.Threading.Tasks;
using AutoMapper;
using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using WebhookReceiver.Models;
using WebhookReceiver.Models.DTOs;
using WebhookReceiver.Services;

namespace WebhookReceiver.Repositories
{
    public class SubscriptionRepository : ISubscriptionRepository
    {
        private readonly IConfiguration _config;
        private readonly ITwitchService _twitchService;
        private readonly IMapper _mapper;

        public SubscriptionRepository(IMapper mapper, IConfiguration config, ITwitchService twitchService)
        {
            _mapper = mapper;
            _config = config;
            _twitchService = twitchService;
        }

        public async Task Upsert(SubscriptionDto subscription)
        {
            SqlConnection conn = new(_config.GetConnectionString("default"));
            string query = $"SELECT * FROM Subscriptions WHERE ChannelID = '{subscription.ChannelId}'";
            string channelName = (await _twitchService.GetChannelData(subscription.ChannelId)).BroadcasterName;
            subscription.ChannelName = channelName;
            Subscription record = await conn.QueryFirstOrDefaultAsync<Subscription>(query);
            if (record == null)
            {
                await conn.InsertAsync(_mapper.Map(subscription, new Subscription()));
            }
            else
            {
                await conn.UpdateAsync(_mapper.Map(subscription, record));
            }
        }

        public async Task Delete(string channelId)
        {
            SqlConnection conn = new(_config.GetConnectionString("default"));
            string query = $"SELECT * FROM Subscriptions WHERE ChannelID = '{channelId}'";
            Subscription record = await conn.QueryFirstOrDefaultAsync<Subscription>(query);
            if (record != null) await conn.DeleteAsync(record);
        }
    }
}