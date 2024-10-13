﻿using RoboLlama.Infrastructure;
using RoboLlamaLibrary.Plugins;
using System.Reflection;
using System.Runtime.Loader;
using System.Timers;

namespace RoboLlama.Services;

public class PluginService : IPluginService
{
	private readonly Dictionary<string, Func<string, IEnumerable<string>>> _triggerWords = new();
	private readonly List<object> _reports = new();
	private readonly IConfiguration _config;
	private readonly List<object> _pluginInstances = new();
	private readonly Dictionary<string, AssemblyLoadContext> assemblyLoadContexts = new();
	private Dictionary<string, ElapsedEventHandler> _timerElapsedHandler = [];
	public PluginService(IConfiguration config)
	{
		_config = config;
	}

	public void LoadPlugins(string pluginDirectory, string rootDirectory)
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
			var assemblyLoadContext = new AssemblyLoadContext(file, true);
			assemblyLoadContext.Resolving += (AssemblyLoadContext loadContext, AssemblyName assemblyName) =>
			{
				// Try to locate the missing assembly in the plugin directory
				string assemblyPath = Path.Combine(pluginDirectory, $"{assemblyName.Name}.dll");
				if (assemblyName.Name == "Microsoft.Data.SqlClient")
				{
					assemblyPath = Path.Combine(rootDirectory, "Microsoft.Data.SqlClient.dll");
				}
				if (File.Exists(assemblyPath))
				{
					return loadContext.LoadFromAssemblyPath(assemblyPath);
				}
				return null;
			};
			assemblyLoadContexts[file] = assemblyLoadContext;
			Assembly pluginAssembly = assemblyLoadContext.LoadFromAssemblyPath(file);
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

	public List<System.Timers.Timer> GetReportingTimers(StreamWriter writer, List<Models.ChannelStatus> channelsToJoin)
	{
		List<System.Timers.Timer> output = new();
		foreach (object reporter in _reports)
		{
			IReportPlugin plugin = (reporter as IReportPlugin)!;
			try
			{
				OnTimedEvent(plugin, writer, channelsToJoin);
				System.Timers.Timer timer = new(plugin.PreferredReportInterval.TotalMilliseconds);
				var classHandle = reporter.GetType().Name;
				if (_timerElapsedHandler.ContainsKey(classHandle))
				{
					timer.Elapsed -= _timerElapsedHandler[classHandle];
				}
				else
				{
					_timerElapsedHandler[classHandle] = (sender, e) => OnTimedEvent(plugin, writer, channelsToJoin);
				}
				timer.Elapsed += _timerElapsedHandler[classHandle];
				timer.AutoReset = true;
				output.Add(timer);
			}
			catch (Exception e)
			{
				BotConsole.WriteErrorLine($"Error Getting Reports from: {plugin.GetType().Name}\n{e.Message}");
			}
		}
		return output;
	}

	private static void OnTimedEvent(IReportPlugin plugin, StreamWriter writer, List<Models.ChannelStatus> channelsToJoin)
	{
		foreach (string report in plugin.GetLatestReports())
		{
			foreach (Models.ChannelStatus? channelStatus in channelsToJoin.Where(x => x.Status == "joined"))
			{
				writer.SayToChannel(channelStatus.Name, report).GetAwaiter().GetResult();
			}
		}
	}
}
