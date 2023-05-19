﻿using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using WebhookReceiver.Models;

namespace WebhookReceiver.Services
{
    public class TwitchService : ITwitchService
    {
        private const string ClientSecret = "v0hqnfvn4fudxuahgtac2i7n4w19f7";
        private const string ClientId = "acq63ijja0o1mtos9sh1zvt09c2174";

        private HttpClient _httpClient;

        public async Task<ChannelData> GetChannelData(string id)
        {
            await SetupHttpClient();
            string url = $"https://api.twitch.tv/helix/channels?broadcaster_id={id}";
            ChannelDataResponse response = await _httpClient.GetFromJsonAsync<ChannelDataResponse>(new Uri(url));
            return response.Data.Count > 0 ? response.Data[0] : null;
        }

        private async Task SetupHttpClient()
        {
            AuthTokenResponse authtoken = await GetAuthToken();
            if (_httpClient == null)
            {
                _httpClient = new HttpClient();
                _httpClient.DefaultRequestHeaders.Add("Client-ID", ClientId);
            }

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", authtoken.AccessToken);
        }

        private static async Task<AuthTokenResponse> GetAuthToken()
        {
            string url =
                $"https://id.twitch.tv/oauth2/token?client_id={ClientId}&client_secret={ClientSecret}&grant_type=client_credentials";
            using HttpClient httpClient = new();
            HttpResponseMessage response = await httpClient.PostAsync(new Uri(url), null);
            string result = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<AuthTokenResponse>(result);
        }
    }
}