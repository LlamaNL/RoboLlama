using RoboLlama.Infrastructure;
using RoboLlamaLibrary.Plugins;
using System.Reflection;

namespace RoboLlama.Services;

public class PluginService : IPluginService
{
    private readonly Dictionary<string, Func<string, IEnumerable<string>>> _triggerWords = new();
    private readonly List<object> _reports = new();
    private readonly IConfiguration _config;
    private readonly List<object> _pluginInstances = new();
    public PluginService(IConfiguration config)
    {
        _config = config;
    }

    public void LoadPlugins(string pluginDirectory)
    {
        // clear in case this is ran for a second time
        _reports.Clear();
        _triggerWords.Clear();
        _pluginInstances.Clear();

        // Get all files in the directory
        string[] allFiles = Directory.GetFiles(pluginDirectory);

        // Filter out those that end with "RoboLlamaPlugin.dll"
        string[] pluginFiles = allFiles.Where(file => Path.GetFileName(file).EndsWith("RoboLlamaPlugin.dll")).ToArray();

        // Now pluginFiles contains all files in the folder that end with "RoboLlamaPlugin.dll"
        foreach (string file in pluginFiles)
        {
            Assembly pluginAssembly = Assembly.LoadFile(file);
            foreach (Type type in pluginAssembly.GetTypes())
            {
                object plugin = null!;
                if (typeof(IPluginConfig).IsAssignableFrom(type))
                {
                    plugin ??= Activator.CreateInstance(type)!;
                    SetPluginConfigIfAvailable(type, plugin);
                }
                if (typeof(ITriggerWordPlugin).IsAssignableFrom(type))
                {
                    plugin ??= Activator.CreateInstance(type)!;
                    foreach (KeyValuePair<string, Func<string, IEnumerable<string>>> triggerWord in (plugin as ITriggerWordPlugin)!.GetTriggerWords())
                    {
                        _triggerWords[triggerWord.Key] = triggerWord.Value;
                    }
                }
                if (typeof(IReportPlugin).IsAssignableFrom(type))
                {
                    plugin ??= Activator.CreateInstance(type)!;
                    _reports.Add(plugin);
                }
                if (plugin is not null)
                {
                    _pluginInstances.Add(plugin);
                }
            }
        }
    }

    private void SetPluginConfigIfAvailable(Type type, object plugin)
    {
        if (typeof(IPluginConfig).IsAssignableFrom(type))
        {
            string className = type.Name;
            Dictionary<string, string?> configDict = _config.GetSection("PluginConfig").GetSection(className).ToDictionary();
            (plugin as IPluginConfig)!.SetConfig(configDict);
        }
    }

    public Dictionary<string, Func<string, IEnumerable<string>>> GetTriggerWords() => _triggerWords;

    public List<string> GetReports()
    {
        List<string> output = new();
        foreach (object reporter in _reports)
        {
            IReportPlugin plugin = (reporter as IReportPlugin)!;
            try
            {
                output.AddRange(plugin.GetLatestReports());
            }
            catch (Exception e)
            {
                BotConsole.WriteErrorLine($"Error Getting Reports from: {plugin.GetType().Name}\n{e.Message}");
            }
        }
        return output;
    }
}
