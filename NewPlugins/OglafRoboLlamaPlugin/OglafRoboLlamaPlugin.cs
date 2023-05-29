using RoboLlamaLibrary.Plugins;
using RoboLlamaRSSReader;

namespace OglafRoboLlamaPlugin;

public class OglafRoboLlamaPlugin : IReportPlugin
{
    public TimeSpan PreferredReportInterval => TimeSpan.FromMinutes(5);

    public List<string> GetLatestReports()
    {
        RoboLlamaRssReader oglaf = new("Oglaf", "http://oglaf.com/feeds/rss/", 5);
        IEnumerable<RssItem> NewItems = oglaf.GetNewItemsAsync().GetAwaiter().GetResult();
        List<string> output = new();
        foreach (RssItem result in NewItems)
        {
            string line = $"[Oglaf] {result.Title} - {result.Url}";
            output.Add(line);
        }
        return output;
    }
}