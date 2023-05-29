using RoboLlama.Models;

namespace RoboLlama.Services;

public interface IPluginService
{
    public void LoadPlugins(string pluginDirectory);
    public Dictionary<string, Func<string, IEnumerable<string>>> GetTriggerWords();
    public List<System.Timers.Timer> GetReportingTimers(StreamWriter writer, List<ChannelStatus> channelsToJoin);
}
