using System.Threading.Tasks;
using AutoMapper;
using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using WebhookReceiver.Models;
using WebhookReceiver.Models.DTOs;

namespace WebhookReceiver.Repositories
{
    public class AlertRepository : IAlertRepository
    {
        private readonly IConfiguration _config;
        private readonly IMapper _mapper;

        public AlertRepository(IMapper mapper, IConfiguration config)
        {
            _mapper = mapper;
            _config = config;
        }

        public async Task Upsert(TwitchStreamAlertDto twitchStreamAlert)
        {
            SqlConnection conn = new(_config.GetConnectionString("default"));
            string query = $"SELECT * FROM TwitchStreamAlerts WHERE ChannelID = '{twitchStreamAlert.ChannelId}'";
            TwitchStreamAlert record = await conn.QueryFirstOrDefaultAsync<TwitchStreamAlert>(query);
            if (record == null)
                await conn.InsertAsync(_mapper.Map(twitchStreamAlert, new TwitchStreamAlert()));
            else
                await conn.UpdateAsync(_mapper.Map(twitchStreamAlert, record));
        }
    }
}