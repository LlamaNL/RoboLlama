using Microsoft.Extensions.Options;
using RoboLlama.Infrastructure;
using RoboLlama.Models;
using RoboLlama.Services;
using System.Net.Sockets;

namespace RoboLlama;

public class Bot : BackgroundService
{
    private readonly IPluginService _pluginService;

    private readonly ServerConfig _config;
    private readonly IrcConnectionPolicy _connectionPolicy = new(20, 10);
    private readonly List<ChannelStatus> _channelsToJoin;

    private string? currentNick = null;
    private bool pluginsLoaded = false;
    private readonly CancellationTokenSource _tokenSource = new();


    Dictionary<string, Func<string, IEnumerable<string>>>? triggers;

    public Bot(
        IOptionsMonitor<ServerConfig> _monitor,
        IPluginService pluginService)
    {
        _config = _monitor.CurrentValue;
        _channelsToJoin = _config.Channels.Select(x => new ChannelStatus(x, "unjoined")).ToList();
        _pluginService = pluginService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _connectionPolicy.ConnectWithRetriesAsync(async () => await RunBot(stoppingToken));
        }
    }

    private async Task RunBot(CancellationToken stoppingToken)
    {
        try
        {
            using TcpClient irc = new();
            await irc.ConnectAsync(_config.ServerAddress, _config.ServerPort);

            using NetworkStream stream = irc.GetStream();
            using StreamReader reader = new(stream);
            using StreamWriter writer = new(stream) { NewLine = "\r\n", AutoFlush = true };

            currentNick = _config.Nicks.GetNextItemInArray(currentNick);
            await writer.SendRawLineAsync($"NICK {currentNick}");
            await writer.SendRawLineAsync($"USER {_config.Owner} 0 * :{_config.Owner}");

            while (!stoppingToken.IsCancellationRequested)
            {
                if (!irc.Connected)
                {
                    throw new Exception("Lost connection to the IRC server");
                }

                string? inputLine = await reader.ReadLineAsync(stoppingToken);
                if (string.IsNullOrWhiteSpace(inputLine)) continue;
                BotConsole.WriteLine($"< {inputLine}");  // Print raw IRC data received

                if (inputLine.Contains("PING"))
                {
                    await writer.SendRawLineAsync(inputLine.Replace("PING", "PONG"));
                    continue;
                }

                string[] splitInput = inputLine.Split(' ');
                if (splitInput.Length > 1)
                {
                    switch (splitInput[1])
                    {
                        case "001": // 001 RPL_WELCOME message received
                            {
                                foreach (ChannelStatus channelStatus in _channelsToJoin)
                                {
                                    await writer.SendRawLineAsync($"JOIN {channelStatus.Name}");
                                }
                            }
                            break;
                        case "366": // end of names list, ive succesfully joined a channel
                            {
                                string channel = splitInput[3];
                                ChannelStatus? chanJoined = _channelsToJoin.SingleOrDefault(x => x.Name == channel);
                                if (chanJoined is not null)
                                {
                                    chanJoined.Status = "joined";
                                }
                                if (_channelsToJoin.All(x => x.Status == "joined") && !pluginsLoaded)
                                {
                                    // start running plugins
                                    EnablePlugins(writer);
                                }
                                break;
                            }
                        case "431": // No nickname given
                            {
                                BotConsole.WriteErrorLine("Error: No nickname given. Please provide a nickname.");
                                throw new Exception("No nickname given");
                            }
                        case "433": // my nickname is in use
                            {
                                currentNick = _config.Nicks.GetNextItemInArray(currentNick);
                                await writer.SendRawLineAsync($"NICK {currentNick}");
                                break;
                            }
                        case "KICK": // someone has been kicked
                            {
                                string channel = splitInput[2];
                                string kickeduser = splitInput[3];
                                if (currentNick is not null && kickeduser == currentNick)
                                {
                                    await writer.SendRawLineAsync($"JOIN {channel}");
                                }
                                break;
                            }
                        case "474": // i am banned
                            {
                                string channelName = splitInput[3];
                                if (_channelsToJoin.Any(x => x.Name == channelName))
                                {
                                    _channelsToJoin.RemoveAll(x => x.Name == channelName);
                                }
                                break;
                            }
                        case "PRIVMSG":
                            {
                                //:<nickname>!<username>@<hostname> COMMAND
                                string nickname = splitInput[0][1..].Split('!')[0];
                                string channel = splitInput[2];
                                string userMessage = inputLine.Split(new[] { " :" }, 2, StringSplitOptions.None)[1];
                                if (userMessage.Contains("VERSION"))
                                {
                                    // The version information of your IRC client
                                    const string versionInfo = "RoboLlama 2.0";

                                    // Send the version information back to the user
                                    string reply = $"NOTICE {nickname} :\x01VERSION {versionInfo}\x01";
                                    await writer.SendRawLineAsync(reply);
                                    break;
                                }
                                string[] userMessageSplit = userMessage.Split(' ');
                                string command = userMessageSplit[0];
                                string[] args;
                                if (splitInput.Length > 2)
                                {
                                    args = userMessageSplit[1..];
                                }
                                if (nickname == _config.AdminNick)
                                {
                                    // Insert Admin Commands here
                                    switch (command)
                                    {
                                        case "!reloadplugins":
                                            {
                                                // run the plugin init again
                                                EnablePlugins(writer);
                                                break;
                                            }
                                        case "!restartbot":
                                            {
                                                throw new Exception("Restarting bot");
                                            }
                                    }
                                }
                                if (!pluginsLoaded || triggers is null) break;
                                foreach (KeyValuePair<string, Func<string, IEnumerable<string>>> trigger in triggers)
                                {
                                    if (!userMessage.Contains(trigger.Key, StringComparison.OrdinalIgnoreCase)) continue;
                                    foreach (string answer in trigger.Value(userMessage))
                                    {
                                        await writer.SayToChannel(channel, answer);
                                    }
                                }
                                break;
                            }
                    }
                }
            }
        }
        catch (OperationCanceledException ex)
        {
            BotConsole.WriteSystemLine("Received signal to terminate program: " + ex.Message);
        }
        catch (SocketException ex)
        {
            BotConsole.WriteErrorLine("Error connecting to IRC server: " + ex.Message);
            throw;
        }
        catch (IOException ex)
        {
            BotConsole.WriteErrorLine("Error reading from or writing to network stream: " + ex.Message);
            throw;
        }
        catch (Exception ex) when (ex.Message == "No nickname given")
        {
            BotConsole.WriteErrorLine("Bot not properly configured - nickname missing!");
            Environment.Exit(431);
        }
        catch (Exception ex)
        {
            BotConsole.WriteErrorLine("Unexpected error: " + ex.Message);
            throw;
        }
    }

    private void EnablePlugins(StreamWriter writer)
    {
        pluginsLoaded = false;
        BotConsole.WriteErrorLine("Reloading Plugins");
        _tokenSource.Cancel();
        _tokenSource.TryReset();
        try
        {
            _pluginService.LoadPlugins(_config.PluginFolder);
            triggers = _pluginService.GetTriggerWords();
            _ = Anouncer(writer, _tokenSource.Token);
            pluginsLoaded = true;
        }
        catch
        {
            pluginsLoaded = false;
        }
    }

    private async Task Anouncer(StreamWriter writer, CancellationToken stoppingToken)
    {
        await Task.Delay(20 * 1000);
        while (pluginsLoaded)
        {
            foreach (string line in _pluginService.GetReports())
            {
                foreach (ChannelStatus? channelStatus in _channelsToJoin.Where(x => x.Status == "joined"))
                {
                    await writer.SayToChannel(channelStatus.Name, line);
                }
            }
            try
            {
                await Task.Delay(1000 * 60 * 5, stoppingToken);
            }
            catch
            {
                pluginsLoaded = false;
            }
        }
    }
}