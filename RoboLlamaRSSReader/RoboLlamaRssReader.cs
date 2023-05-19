using System.Net;
using System.ServiceModel.Syndication;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace RoboLlamaRSSReader;

public sealed class RoboLlamaRssReader
{
    private readonly string _feedUrl;
    private readonly FileInfo _historyFile;
    private readonly int _maxitems;

    public RoboLlamaRssReader(string name, string feedurl, int maxitems)
    {
        _historyFile = new FileInfo(name.ToLower() + "-history.json");
        _feedUrl = feedurl;
        _maxitems = maxitems;
    }

    public async Task<IEnumerable<RssItem>> GetNewItemsAsync()
    {
        IEnumerable<RssItem> feed = await GetFeedAsync(_feedUrl);
        IEnumerable<RssItem> history = GetHistory();
        if (feed is null || history is null) return Enumerable.Empty<RssItem>();

        // remove any duplicates from feed so i only get the new ones
        IEnumerable<RssItem> newitems = feed.Where(x => history.All(y => y.Id != x.Id));

        // Save the new ones
        List<RssItem> rssItems = newitems.ToList();
        SaveHistory(history, rssItems);

        // total new items exceeds _maxitems return max else return all;
        return rssItems.Count >= _maxitems ? rssItems.Take(_maxitems) : rssItems;
    }

    private IEnumerable<RssItem> GetHistory()
    {
        if (!_historyFile.Exists)
        {
            return Enumerable.Empty<RssItem>();
        }
        else
        {
            IEnumerable<RssItem>? result = JsonSerializer.Deserialize<IEnumerable<RssItem>>(File.ReadAllText(_historyFile.FullName));
            return result ?? Enumerable.Empty<RssItem>();
        }
    }

    private void SaveHistory(IEnumerable<RssItem> olditems, IEnumerable<RssItem> newitems)
    {
        IEnumerable<RssItem> allitems = olditems.Concat(newitems);

        string json = JsonSerializer.Serialize(allitems, new JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(_historyFile.FullName, json);
    }

    private async Task<IEnumerable<RssItem>> GetFeedAsync(string feedurl)
    {
        ServicePointManager.Expect100Continue = true;
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        string xmlstring;
        using (HttpClient httpClient = new())
        {
            try
            {
                HttpResponseMessage response = await httpClient.GetAsync(new Uri(feedurl));
                if (response.IsSuccessStatusCode)
                {
                    xmlstring = await response.Content.ReadAsStringAsync();
                }
                else
                {
                    return Enumerable.Empty<RssItem>();
                }
            }
            catch (HttpRequestException e)
            {
                Console.Error.WriteLine(e);
                return Enumerable.Empty<RssItem>();
            }
        }

        XDocument xDoc = XDocument.Parse(xmlstring);

        // Removed cache element, SyndicationFeed chokes on it
        // Add more elements here if needed
        xDoc.Descendants("cache").Remove();

        xmlstring = xDoc.ToString();

        XmlReader reader = XmlReader.Create(new StringReader(xmlstring));
        SyndicationFeed feed = SyndicationFeed.Load(reader);
        reader.Close();
        return CreateItemList(feed);
    }

    private IEnumerable<RssItem> CreateItemList(SyndicationFeed feed)
    {
        return feed.Items.OrderByDescending(x => x.PublishDate)
            .Take(_maxitems)
            .Select(
                syndicationItem =>
                    new RssItem
                    {
                        Id = syndicationItem.Id,
                        Title = syndicationItem.Title.Text,
                        Url = syndicationItem.Links[0].Uri
                    });
    }
}