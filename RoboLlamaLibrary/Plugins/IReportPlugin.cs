namespace RoboLlamaLibrary.Plugins;

public interface IReportPlugin
{
    public List<string> GetLatestReports();

    public TimeSpan PreferredReportInterval { get; }
}
