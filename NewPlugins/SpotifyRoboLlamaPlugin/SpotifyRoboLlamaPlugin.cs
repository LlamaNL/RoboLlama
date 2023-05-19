using System.Net.Http.Headers;
using HtmlAgilityPack;
using RoboLlamaLibrary.Plugins;

namespace SpotifyRoboLlamaPlugin;

public class SpotifyRoboLlamaPlugin : ITriggerWordPlugin
{
    public static IEnumerable<string> GetResponse(string input)
    {
        List<string> output = new();
        try
        {
            string? result = Parse(input);
            if (result is not null) output.Add($"[Spotify] {result}");
        }
        catch
        {
            // do nothing
        }

        return output;
    }

    public static string? Parse(string spotify)
    {
        try
        {
            if (string.IsNullOrEmpty(spotify)) return null;
            if (spotify.StartsWith("spotify:track:", StringComparison.InvariantCulture))
                spotify = "https://open.spotify.com/track/" + spotify.Remove(0, 14);
            if (spotify.IndexOf("spotify.com", StringComparison.OrdinalIgnoreCase) < 0) return null;
            using HttpClient httpClient = new();

            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RoboLlama", "1.0"));
            httpClient.DefaultRequestHeaders.Connection.Add("keep-alive");

            HttpResponseMessage response = httpClient.GetAsync(new Uri(spotify)).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode) return null;
            string html = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            HtmlDocument doc = new();
            doc.LoadHtml(html);
            return doc.DocumentNode.SelectSingleNode("//head/title").InnerHtml;
        }
        catch
        {
            return null;
        }
    }

    public Dictionary<string, Func<string, IEnumerable<string>>> GetTriggerWords()
    {
        return new Dictionary<string, Func<string, IEnumerable<string>>>
        {
            ["spotify"] = (message) => GetResponse(message)
        };
    }
}