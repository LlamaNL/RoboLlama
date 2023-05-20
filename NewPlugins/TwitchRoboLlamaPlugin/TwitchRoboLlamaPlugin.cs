using RoboLlamaLibrary.Plugins;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;
using Microsoft.Data.SqlClient;
using Dapper.Contrib.Extensions;
using Dapper;
using RoboLlamaLibrary.Models;
using RoboLlamaLibrary.Infrastructure;

namespace TwitchRoboLlamaPlugin;

public class TwitchRoboLlamaPlugin : ITriggerWordPlugin, IReportPlugin, IPluginConfig
{
    private Dictionary<string, string?>? _config;
    private string? _connectionString;
    private string? _clientId;
    private string? _clientSecret;
    private string? _callbackSecret;
    private string? _callbackUrl;

    public List<string> GetLatestReports()
    {
        var output = new List<string>();
        SqlConnection conn = new(_connectionString);
        foreach (TwitchStreamAlert alert in conn.GetAllAsync<TwitchStreamAlert>().GetAwaiter().GetResult().Where(alert => !alert.Announced))
        {
            string? game = GetGame(alert.GameId).GetAwaiter().GetResult();
            StringBuilder sb = new();
            sb.Append($"[https://www.twitch.tv/{alert.UserName}]".ColorFormat(IrcColor.Violet,
                IrcColor.Black));
            sb.Append(" - ").Append(alert.Title.Replace("\n", "")).Append(" - ");
            if (game != null) sb.Append(game);
            sb.Append(" - LIVE");
            output.Add(sb.ToString());
            alert.Announced = true;
            conn.UpdateAsync(alert).GetAwaiter().GetResult();
        }
        return output;
    }

    public Dictionary<string, Func<string, IEnumerable<string>>> GetTriggerWords()
    {
        return new Dictionary<string, Func<string, IEnumerable<string>>>
        {
            ["!twitchadd"] = (message) => TwitchAdd(message),
            ["!twitchremove"] = (message) => TwitchRemove(message),
            ["!twitchsubs"] = (_) => TwitchSubs()
        };
    }

    void IPluginConfig.SetConfig(Dictionary<string, string?> config)
    {
        _config = config;
        _connectionString = _config["ConnectionString"];
        _clientId = _config["ClientId"];
        _clientSecret = _config["ClientSecret"];
        _callbackUrl = _config["CallbackUrl"];
        _callbackSecret = _config["CallbackSecret"];
    }

    private IEnumerable<string> TwitchAdd(string input)
    {
        string? id = GetChannelId(input).GetAwaiter().GetResult();
        if (id is null) return new List<string> { $"[Twitch] {input} is not a channel" };
        Subscribe(id).GetAwaiter().GetResult();
        return Enumerable.Empty<string>();
    }

    private IEnumerable<string> TwitchRemove(string input)
    {
        string? subscriptionId = GetSubscriptionId(input).GetAwaiter().GetResult();
        if (subscriptionId is null) return new List<string>() { "Invalid subscription" };
        Unsubscribe(subscriptionId).GetAwaiter().GetResult();
        return new List<string>() { $"Deleted {input}" };
    }

    private List<string> TwitchSubs()
    {
        return new List<string> { string.Join(", ", GetChannels()) };
    }

    private async Task<HttpClient> SetupHttpClient()
    {
        AuthTokenResponse? authtoken = await GetAuthToken();
        if (authtoken is null) throw new ArgumentNullException(nameof(authtoken));
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Client-ID", _config!["ClientId"]);

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authtoken.AccessToken);
        return httpClient;
    }

    public async Task<string?> GetSubscriptionId(string name)
    {
        SqlConnection conn = new(_connectionString);
        IEnumerable<Subscription> subscriptions = await conn.GetAllAsync<Subscription>();
        Subscription? channel = subscriptions.SingleOrDefault(x => x.ChannelName == name);
        if (channel is null) return null;
        return channel.SubscriptionId;
    }

    public async Task<string?> GetChannelId(string name)
    {
        var httpClient = await SetupHttpClient();
        string url = $"https://api.twitch.tv/helix/users?login={name}";
        HttpResponseMessage response = await httpClient.GetAsync(url);
        string content = await response.Content.ReadAsStringAsync();
        ChannelDataResponse? json = JsonSerializer.Deserialize<ChannelDataResponse>(content);
        return json is not null && json?.data.Length > 0 ? json.data[0].id : null;
    }

    public async Task Subscribe(string id)
    {
        var httpClient = await SetupHttpClient();
        const string url = "https://api.twitch.tv/helix/eventsub/subscriptions";

        using StringContent stringcontent = new(CreateSubscribeRequest(id), Encoding.UTF8, "application/json");
        await httpClient.PostAsync(new Uri(url), stringcontent);
    }

    private string CreateSubscribeRequest(string id)
    {
        StringBuilder sb = new();
        sb.AppendLine("{");
        sb.AppendLine(@"    ""type"": ""stream.online"",");
        sb.AppendLine(@"    ""version"": ""1"",");
        sb.AppendLine(@"    ""condition"": {");
        sb.Append(@"        ""broadcaster_user_id"": """).Append(id).AppendLine(@"""");
        sb.AppendLine("    },");
        sb.AppendLine(@"    ""transport"": {");
        sb.AppendLine(@"        ""method"": ""webhook"",");
        sb.Append(@"        ""callback"": """).Append(_callbackUrl).AppendLine(@""",");
        sb.Append(@"        ""secret"": """).Append(_callbackSecret).AppendLine(@"""");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    public async Task ResubAll()
    {
        SqlConnection conn = new(_connectionString);
        foreach (Subscription subscription in conn.GetAll<Subscription>()) await Subscribe(subscription.ChannelId);
    }

    public async Task Unsubscribe(string subscriptionId)
    {
        var httpClient = await SetupHttpClient();
        string url = $"https://api.twitch.tv/helix/eventsub/subscriptions?id={subscriptionId}";
        _ = await httpClient.DeleteAsync(url);
        DeleteSubscription(subscriptionId);
    }

    public void SetAnnounced(string channelid)
    {
        const string query = "UPDATE Subscription SET Announced=1 WHERE ChannelID=@channelid";
        SqlConnection conn = new(_connectionString);
        conn.Execute(query, new { channelid });
    }

    private async Task<AuthTokenResponse?> GetAuthToken()
    {
        string url =
            $"https://id.twitch.tv/oauth2/token?client_id={_clientId}&client_secret={_clientSecret}&grant_type=client_credentials";
        using HttpClient httpClient = new();
        HttpResponseMessage response = await httpClient.PostAsync(new Uri(url), new StringContent(string.Empty));
        string result = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<AuthTokenResponse>(result);
    }

    public async Task<string?> GetGame(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        var httpClient = await SetupHttpClient();
        string url = $"https://api.twitch.tv/helix/games?id={id}";
        GameDataResponse? response = await httpClient.GetFromJsonAsync<GameDataResponse>(new Uri(url));
        return response?.Data.Count == 1 ? response.Data[0].Name : null;
    }

    public IEnumerable<string> GetChannels()
    {
        SqlConnection conn = new(_connectionString);
        IEnumerable<Subscription> channels = conn.GetAll<Subscription>();
        return channels.Select(x => x.ChannelName);
    }

    public void DeleteSubscription(string subscriptionId)
    {
        SqlConnection conn = new(_connectionString);
        string query = $"SELECT TOP 1 * FROM Subscriptions WHERE SubscriptionId='{subscriptionId}'";
        Subscription record = conn.QuerySingleOrDefault<Subscription>(query);
        if (record is not null)
        {
            conn.Delete(record);
        }
    }
}