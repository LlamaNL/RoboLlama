using RoboLlamaLibrary.Plugins;
using System.Text.Json;
using System.Web;

namespace UrbanDictionaryRoboLlamaPlugin;
public class UrbanDictionaryRoboLlamaPlugin : ITriggerWordPlugin
{
    public static IEnumerable<string> GetResponse(string input)
    {
        List<string> output = new();
        try
        {
            UrbanDictionaryResult? result = GetFirstOrDefaultResult(input);
            if (result is not null) output.Add($"[UrbanDictionary] {result.Definition} - Example: {result.Example}");
        }
        catch
        {
            // do nothing
        }
        return output;
    }

    private static UrbanDictionaryResult? GetFirstOrDefaultResult(string input)
    {
        HttpClient client = new();
        string encodedsearch = HttpUtility.UrlEncode(input);
        string url = $"http://api.urbandictionary.com/v0/define?term={encodedsearch}";
        HttpResponseMessage response = client.GetAsync(url).GetAwaiter().GetResult();
        if (response.IsSuccessStatusCode)
        {
            string result = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            UrbanDictionaryCollection results = JsonSerializer.Deserialize<UrbanDictionaryCollection>(result)!;
            if (results.List?.Any() == true) return results.List.OrderByDescending(x => x.ThumbsUp).First();
        }

        return null;
    }

    public Dictionary<string, Func<string, IEnumerable<string>>> GetTriggerWords()
    {
        return new Dictionary<string, Func<string, IEnumerable<string>>>
        {
            ["!ud"] = (message) => GetResponse(message)
        };
    }
}