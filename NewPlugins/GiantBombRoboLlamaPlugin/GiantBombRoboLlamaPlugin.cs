using HtmlAgilityPack;
using RoboLlamaLibrary.Models;
using RoboLlamaLibrary.Plugins;
using RoboLlamaLibrary.Infrastructure;
using RoboLlamaRSSReader;
using System.Text.RegularExpressions;

namespace GiantBombRoboLlamaPlugin;

public class GiantBombRoboLlamaPlugin : IReportPlugin
{
    private static string GetCast(Uri url)
    {
        using HttpClient httpClient = new();
        try
        {
            HttpResponseMessage response = httpClient.GetAsync(url).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode) return string.Empty;

            string html = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            HtmlDocument doc = new();
            doc.LoadHtml(html);
            HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes("//*[text()[contains(., 'Cast:')]]");
            if (nodes == null) return "";
            string text = nodes[0].InnerText.Replace("\n", string.Empty, StringComparison.InvariantCulture).Trim();
            const RegexOptions options = RegexOptions.None;
            Regex regex = new("[ ]{2,}", options);
            return regex.Replace(text, " ");
        }
        catch (HttpRequestException e)
        {
            Console.Error.WriteLine(e);
            return string.Empty;
        }
    }

    public List<string> GetLatestReports()
    {
        List<string> outputList = new();
        RoboLlamaRssReader giantbomb = new("GiantBomb", "https://www.giantbomb.com/feeds/mashup/", 5);
        IEnumerable<RssItem> newItems = giantbomb.GetNewItemsAsync().GetAwaiter().GetResult();
        foreach (RssItem result in newItems)
        {
            string linkshekje = "[".ColorFormat(IrcColor.White, IrcColor.Black);
            string giant = "Giant".ColorFormat(IrcColor.Red, IrcColor.Black);
            string bomb = "Bomb".ColorFormat(IrcColor.White, IrcColor.Black);
            string rechtshekje = "]".ColorFormat(IrcColor.White, IrcColor.Black);
            string header = linkshekje + IrcControlCode.Bold + giant + bomb + IrcControlCode.Bold + rechtshekje;

            string output = $"{header} {result.Title}";

            string text = GetCast(result.Url);
            if (!string.IsNullOrEmpty(text)) output += " - " + text;

            output +=
                $" [{IrcControlCode.Underline2}{IrcControlCode.Bold}{result.Url}{IrcControlCode.Underline2}{IrcControlCode.Bold}]";
            outputList.Add(output);
        }
        return outputList;
    }
}