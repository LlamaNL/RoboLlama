using RoboLlamaLibrary.Plugins;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MastodonRoboLlamaPlugin;

public class MastodonRoboLlamaPlugin : ITriggerWordPlugin
{
    public Dictionary<string, Func<string, IEnumerable<string>>> GetTriggerWords()
    {
        return new Dictionary<string, Func<string, IEnumerable<string>>>
        {
            ["@"] = (message) => GetResponse(message)
        };
    }

    public static IEnumerable<string> GetResponse(string input)
    {
        List<string> output = new();
        try
        {
            //https://toot.community/@RapidOffensiveUnit@mastodonapp.uk/109884859812195397
            string[] spaceSplit = input.Split(' ');
            foreach (var space in spaceSplit)
            {
                string[] splits = space.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (!IsDigitsOnly(splits[3]))
                {
                    return output;
                }
                string url = $"https://{splits[1]}/api/v1/statuses/{splits[3]}";
                HttpClient httpClient = new();
                HttpResponseMessage response = httpClient.GetAsync(url).GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    return output;
                }
                string content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                MastodonResponse mastodonResponse = JsonSerializer.Deserialize<MastodonResponse>(content)!;
                string mastodonContent = "[Mastodon] " + mastodonResponse.content.Replace("<p>", "").Replace("</p>", "\n");
                mastodonContent = StripHTML(mastodonContent);
                output.AddRange(mastodonContent.Split('\n'));
                output.AddRange(mastodonResponse.media_attachments.Select(x => x.url));
            }
        }
        catch
        {
            return output;
        }
        return output;
    }

    private static bool IsDigitsOnly(string str)
    {
        foreach (char c in str)
        {
            if (c < '0' || c > '9')
                return false;
        }

        return true;
    }

    public static string StripHTML(string input)
    {
        return Regex.Replace(input, "<.*?>", String.Empty);
    }
}