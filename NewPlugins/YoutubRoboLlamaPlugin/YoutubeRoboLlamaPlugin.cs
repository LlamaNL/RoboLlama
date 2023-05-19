using System.Web;
using System.Xml;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using RoboLlamaLibrary.Models;
using RoboLlamaLibrary.Plugins;
using RoboLlamaLibrary.Infrastructure;

namespace YoutubRoboLlamaPlugin;

public class YoutubeRoboLlamaPlugin : ITriggerWordPlugin, IPluginConfig
{
    Dictionary<string, string?>? _config;

    public IEnumerable<string> GetResponse(string input)
    {
        List<string> output = new();
        try
        {
            string? id = GetYouTubeVideoId(input);
            if (id is null) return output;
            string? result = GetVideo(id);
            if (result is not null) output.Add(result);
        }
        catch
        {
            // do nothing;
        }

        return output;
    }

    public static string? GetYouTubeVideoId(string youtube)
    {
        Uri uri = new(youtube);

        if (uri.Host != "youtu.be" && uri.Host != "www.youtu.be" && uri.Host != "www.youtube.com" &&
            uri.Host != "youtube.com")
        {
            return null;
        }

        System.Collections.Specialized.NameValueCollection query = HttpUtility.ParseQueryString(uri.Query);

        return query.AllKeys.Contains("v") ? query["v"] : uri.Segments.Last();
    }

    private string? GetVideo(string id)
    {
        YouTubeService youtubeService = new(new BaseClientService.Initializer
        {
            ApiKey = _config!["YoutubeApiKey"]
        });

        VideosResource.ListRequest request = youtubeService.Videos.List("snippet,contentDetails,localizations");
        request.Id = id;
        request.Locale = "en_US";
        Google.Apis.YouTube.v3.Data.VideoListResponse result = request.Execute();

        if (result.Items.Count == 0) return null;
        Google.Apis.YouTube.v3.Data.Video video = result.Items[0];
        string author = video.Snippet.ChannelTitle;
        string title = video.Localizations?.ContainsKey("en") == true ? video.Localizations["en"].Title : video.Snippet.Title;
        TimeSpan duration = XmlConvert.ToTimeSpan(result.Items[0].ContentDetails.Duration);
        LiveStreamState liveStream = (LiveStreamState)Enum.Parse(typeof(LiveStreamState),
            result.Items[0].Snippet.LiveBroadcastContent, true);

        string linkshekje = "[".ColorFormat(IrcColor.White, IrcColor.Black);
        string you = "You".ColorFormat(IrcColor.White, IrcColor.Black);
        string tube = "Tube".ColorFormat(IrcColor.Black, IrcColor.Red);
        string rechtshekje = "]".ColorFormat(IrcColor.White, IrcColor.Black);
        string header = linkshekje + IrcControlCode.Bold + you + tube + IrcControlCode.Bold + rechtshekje;
        string output = $"{header} {title} ({author}) ";
        string time = liveStream switch
        {
            LiveStreamState.Live => "LIVE!",
            LiveStreamState.Upcoming => "Upcoming",
            LiveStreamState.None => duration.ToMyFormat(),
            _ => string.Empty
        };

        output += $"[{IrcControlCode.Bold}{time}{IrcControlCode.Bold}]";
        return output;
    }

    public Dictionary<string, Func<string, IEnumerable<string>>> GetTriggerWords()
    {
        return new Dictionary<string, Func<string, IEnumerable<string>>>
        {
            ["youtu.be"] = (message) => GetResponse(message),
            ["youtube.com"] = (message) => GetResponse(message),
        };
    }

    public void SetConfig(Dictionary<string, string?> config)
    {
        _config = config;
    }

    private enum LiveStreamState
    {
        Live,
        Upcoming,
        None
    }
}