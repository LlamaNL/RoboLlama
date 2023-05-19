using RoboLlamaLibrary.Plugins;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace TwitterRoboLlamaPlugin;

public class TwitterRoboLlamaPlugin : ITriggerWordPlugin, IPluginConfig
{
    private Dictionary<string, string?>? _config;

    public Dictionary<string, Func<string, IEnumerable<string>>> GetTriggerWords()
    {
        return new Dictionary<string, Func<string, IEnumerable<string>>>
        {
            ["twitter"] = (message) => GetResponse(message),
            ["nitter"] = (message) => GetResponse(message)
        };
    }

    public void SetConfig(Dictionary<string, string?> config)
    {
        _config = config;
    }

    public IEnumerable<string> GetResponse(string input)
    {
        List<string> output = new();
        try
        {
            string? result = Parse(input);
            if (result is not null) output.AddRange(result.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));
        }
        catch
        {
            // do nothing;
        }

        return output;
    }

    private string? Parse(string tweet)
    {
        Regex regex = new(@"(twitter|nitter)\.(com|net)\/.*\/status(?:es)?\/([^\/\?]+)",
            RegexOptions.IgnoreCase);
        Match match = regex.Match(tweet);
        if (!match.Success) return null;
        string id = match.Groups[3].Value;

        using HttpClient httpClient = new();
        if (_config is null) return null;
        httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + _config["BearerToken"]);
        try
        {
            string url = $"https://api.twitter.com/2/tweets?ids={id}";
            HttpResponseMessage response = httpClient.GetAsync(new Uri(url)).GetAwaiter().GetResult();
            string content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (response.IsSuccessStatusCode)
            {
                TwitterResponse result = JsonSerializer.Deserialize<TwitterResponse>(content)!;
                string text = HttpUtility.HtmlDecode(result.data[0].text);
                return $"[Tweet] {text}";
            }

            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}
