using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.Data.SqlClient;
using RoboLlamaLibrary.Infrastructure;
using RoboLlamaLibrary.Models;
using RoboLlamaLibrary.Plugins;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TwitchRoboLlamaPlugin;

public class TwitchRoboLlamaPlugin : ITriggerWordPlugin, IReportPlugin, IPluginConfig
{
	private Dictionary<string, string?>? _config;
	private string? _connectionString;
	private string? _clientId;
	private string? _clientSecret;
	private string? _callbackSecret;
	private string? _callbackUrl;

	public TimeSpan PreferredReportInterval => TimeSpan.FromMinutes(1);

	public List<string> GetLatestReports()
	{
		List<string> output = new();
		SqlConnection conn = new(_connectionString);
		foreach (Subscription? subscription in conn.GetAllAsync<Subscription>().GetAwaiter().GetResult().Where(x => !x.Announced))
		{
			string? channel = subscription.ChannelName;
			output.Add($"[Twitch] Subscribed to {channel}!");
			subscription.Announced = true;
			conn.UpdateAsync(subscription).GetAwaiter().GetResult();
		}
		foreach (TwitchStreamAlert alert in conn.GetAllAsync<TwitchStreamAlert>().GetAwaiter().GetResult().Where(alert => !alert.Announced))
		{
			string? game = GetGame(alert.GameId).GetAwaiter().GetResult();
			StringBuilder sb = new();
			sb.Append($"[https://www.twitch.tv/{alert.UserName}]".ColorFormat(IrcColor.Violet, null!));
			sb.Append(" - ").Append(alert.Title.Replace("\n", ""));
			if (game is not null && !alert.Title.Contains(game, StringComparison.OrdinalIgnoreCase))
			{
				sb.Append(" - ").Append(game);
			}
			sb.Append(" - LIVE");
			output.Add(sb.ToString());
			alert.Announced = true;
			conn.UpdateAsync(alert).GetAwaiter().GetResult();
		}
		foreach (TwitchStreamAlert alert in conn.GetAllAsync<TwitchStreamAlert>().GetAwaiter().GetResult().Where(alert => alert.TitleChanged))
		{
			if (CheckOnline(alert.ChannelId).GetAwaiter().GetResult())
			{
				string? game = GetGame(alert.GameId).GetAwaiter().GetResult();
				StringBuilder sb = new();
				sb.Append($"[https://www.twitch.tv/{alert.UserName}]".ColorFormat(IrcColor.Violet, null!));
				sb.Append(" - ").Append(alert.Title.Replace("\n", ""));
				if (game is not null && !alert.Title.Contains(game, StringComparison.OrdinalIgnoreCase))
				{
					sb.Append(" - ").Append(game);
				}
				sb.Append(" - Channel Update");
				output.Add(sb.ToString());
			}
			alert.TitleChanged = false;
			conn.UpdateAsync(alert).GetAwaiter().GetResult();
		}
		return output;
	}

	public Dictionary<string, Func<string, IEnumerable<string>>> GetTriggerWords()
	{
		var output = new Dictionary<string, Func<string, IEnumerable<string>>>
		{
			["!twitchadd"] = (message) => TwitchAdd(message),
			["!twitchremove"] = (message) => TwitchRemove(message),
			["!twitchsubs"] = (_) => TwitchSubs(),
			["!resub"] = (_) => { ResubAll().GetAwaiter().GetResult(); return new List<string>() { "[Twitch] Resubbed all channels" }; }
		};

		return output;
	}

	private async Task UpdateExistingSubsAsync()
	{
		var client = await SetupHttpClient();
		var url = "https://api.twitch.tv/helix/eventsub/subscriptions";
		var response = await client.GetAsync(url);
		response.EnsureSuccessStatusCode();
		var content = await response.Content.ReadAsStringAsync();
		var jsonObject = JsonObject.Parse(content);
		var subs = jsonObject["data"].Deserialize<List<TwitchSubscription>>();
		await Upsert(subs);
	}

	public async Task Upsert(List<TwitchSubscription> subscriptions)
	{
		SqlConnection conn = new(_connectionString);
		foreach (var subGroup in subscriptions.GroupBy(x => x.Condition.BroadcasterUserId))
		{
			var sub = subGroup.First();
			string query = $"SELECT * FROM Subscriptions WHERE ChannelID = '{sub.Condition.BroadcasterUserId}'";
			string channelName = await GetChannelName(sub.Condition.BroadcasterUserId);
			Subscription record = await conn.QueryFirstOrDefaultAsync<Subscription>(query);
			if (record != null)
			{
				continue;
			}

			var newSub = new Subscription()
			{
				ChannelName = channelName,
				ChannelId = sub.Condition.BroadcasterUserId,
				SubscriptionId = subGroup.FirstOrDefault(x => x.Type == "stream.online")?.Id,
				UpdateId = subGroup.FirstOrDefault(x => x.Type == "channel.update")?.Id,
				RegistryDate = DateTime.Now,
				Announced = true
			};
			await conn.InsertAsync(newSub);
		}
	}

	public async Task<string> GetChannelName(string id)
	{
		var client = await SetupHttpClient();
		string url = $"https://api.twitch.tv/helix/channels?broadcaster_id={id}";
		var response = await client.GetAsync(url);
		var content = await response.Content.ReadAsStringAsync();
		var jsonDocument = JsonDocument.Parse(content);
		JsonElement root = jsonDocument.RootElement;

		// Check if the "data" property exists and is an array
		if (root.TryGetProperty("data", out JsonElement dataArray) && dataArray.ValueKind == JsonValueKind.Array)
		{
			// Ensure the array has at least one element
			if (dataArray.GetArrayLength() > 0)
			{
				// Get the first element of the array
				JsonElement firstElement = dataArray[0];

				// Access "broadcaster_name" from the first element
				if (firstElement.TryGetProperty("broadcaster_name", out JsonElement broadcasterElement))
				{
					return broadcasterElement.GetString(); // Return the broadcaster_name value as a string
				}
			}
		}
		throw new ArgumentNullException();
	}

	void IPluginConfig.SetConfig(Dictionary<string, string?> config)
	{
		_config = config;
		_connectionString = _config["ConnectionString"];
		_clientId = _config["ClientId"];
		_clientSecret = _config["ClientSecret"];
		_callbackUrl = _config["CallbackUrl"];
		_callbackSecret = _config["CallbackSecret"];
		UpdateExistingSubsAsync().GetAwaiter().GetResult();
	}

	private IEnumerable<string> TwitchAdd(string input)
	{
		try
		{
			string channel = input.Split(' ')[1];
			string? id = GetChannelId(channel).GetAwaiter().GetResult();
			if (id is null) return new List<string> { $"[Twitch] {channel} is not a channel" };
			Subscribe(id).GetAwaiter().GetResult();
			return new List<string>() { $"[Twitch] Send subscribe request for {channel} to Twitch" };
		}
		catch
		{
			return new List<string>() { "[Twitch] Invalid Prompt" };
		}
	}

	private IEnumerable<string> TwitchRemove(string input)
	{
		try
		{
			string channel = input.Split(' ')[1];
			string[]? subscriptionId = GetSubscriptionId(channel).GetAwaiter().GetResult();
			if (subscriptionId is null) return new List<string>() { "[Twitch] Invalid subscription" };
			foreach (var subId in subscriptionId)
			{
				Unsubscribe(subId).GetAwaiter().GetResult();
			}
			DeleteSubscription(channel);
			return new List<string>() { $"[Twitch] Deleted {channel}" };
		}
		catch
		{
			return new List<string>() { "[Twitch] Invalid Prompt" };
		}
	}

	private List<string> TwitchSubs()
	{
		return new List<string> { $"[Twitch] {string.Join(", ", GetChannels())}" };
	}

	private async Task<HttpClient> SetupHttpClient()
	{
		AuthTokenResponse? authtoken = await GetAuthToken();
		if (authtoken is null) throw new ArgumentNullException(nameof(authtoken));
		HttpClient httpClient = new();
		httpClient.DefaultRequestHeaders.Add("Client-ID", _config!["ClientId"]);

		httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authtoken.AccessToken);
		return httpClient;
	}

	private async Task<string[]?> GetSubscriptionId(string name)
	{
		SqlConnection conn = new(_connectionString);
		IEnumerable<Subscription> subscriptions = await conn.GetAllAsync<Subscription>();
		Subscription? channel = subscriptions.SingleOrDefault(x => x.ChannelName == name);
		if (channel is null) return null;
		return new string[] { channel.SubscriptionId, channel.UpdateId };
	}

	private async Task<string?> GetChannelId(string name)
	{
		HttpClient httpClient = await SetupHttpClient();
		string url = $"https://api.twitch.tv/helix/users?login={name}";
		HttpResponseMessage response = await httpClient.GetAsync(url);
		string content = await response.Content.ReadAsStringAsync();
		ChannelDataResponse? json = JsonSerializer.Deserialize<ChannelDataResponse>(content);
		return json is not null && json?.data.Length > 0 ? json.data[0].id : null;
	}

	private async Task Subscribe(string id)
	{
		HttpClient httpClient = await SetupHttpClient();
		const string url = "https://api.twitch.tv/helix/eventsub/subscriptions";

		using StringContent stringcontent = new(CreateSubscribeRequest(id), Encoding.UTF8, "application/json");
		await httpClient.PostAsync(new Uri(url), stringcontent);
		using StringContent stringcontent2 = new(CreateSubscribeChannelTitleRequest(id), Encoding.UTF8, "application/json");
		await httpClient.PostAsync(new Uri(url), stringcontent2);
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

	private string CreateSubscribeChannelTitleRequest(string id)
	{
		StringBuilder sb = new();
		sb.AppendLine("{");
		sb.AppendLine(@"    ""type"": ""channel.update"",");
		sb.AppendLine(@"    ""version"": ""2"",");
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

	private async Task ResubAll()
	{
		SqlConnection conn = new(_connectionString);
		foreach (Subscription subscription in conn.GetAll<Subscription>()) await Subscribe(subscription.ChannelId);
	}

	private async Task Unsubscribe(string subscriptionId)
	{
		HttpClient httpClient = await SetupHttpClient();
		string url = $"https://api.twitch.tv/helix/eventsub/subscriptions?id={subscriptionId}";
		var response = await httpClient.DeleteAsync(url);
		var content = await response.Content.ReadAsStringAsync();		
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

	private async Task<string?> GetGame(string id)
	{
		if (string.IsNullOrWhiteSpace(id)) return null;
		HttpClient httpClient = await SetupHttpClient();
		string url = $"https://api.twitch.tv/helix/games?id={id}";
		GameDataResponse? response = await httpClient.GetFromJsonAsync<GameDataResponse>(new Uri(url));
		return response?.Data.Count == 1 ? response.Data[0].Name : null;
	}

	private async Task<bool> CheckOnline(string id)
	{
		if (string.IsNullOrWhiteSpace(id)) return false;
		HttpClient httpClient = await SetupHttpClient();
		string url = $"https://api.twitch.tv/helix/streams?user_id={id}";
		GameDataResponse? response = await httpClient.GetFromJsonAsync<GameDataResponse>(new Uri(url));
		return response?.Data.Count == 1;
	}

	private IEnumerable<string> GetChannels()
	{
		SqlConnection conn = new(_connectionString);
		IEnumerable<Subscription> channels = conn.GetAll<Subscription>();
		return channels.Select(x => x.ChannelName);
	}

	private void DeleteSubscription(string channelName)
	{
		SqlConnection conn = new(_connectionString);
		string query = $"SELECT TOP 1 * FROM Subscriptions WHERE ChannelName='{channelName}'";
		Subscription record = conn.QuerySingleOrDefault<Subscription>(query);
		if (record is not null)
		{
			conn.Delete(record);
		}
	}
}