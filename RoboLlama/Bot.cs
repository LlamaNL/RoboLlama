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
    Dictionary<string, Func<string, IEnumerable<string>>> triggers = new();
    private TcpClient? irc;
    private Timer? pingTimer;
    List<System.Timers.Timer>? _timers;
    private bool _reconnecting;
    public Bot(
        IOptionsMonitor<ServerConfig> _monitor,
        IPluginService pluginService,
        ILogger<Bot> logger)
    {
        _config = _monitor.CurrentValue;
        Directory.SetCurrentDirectory(_config.Root);
        _channelsToJoin = _config.Channels.Select(x => new ChannelStatus(x, "unjoined")).ToList();
        _pluginService = pluginService;
        BotConsole.Logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _connectionPolicy.ConnectWithRetriesAsync(async () =>
            {
                await RunBot(stoppingToken);
            });
        }
    }

    private async Task RunBot(CancellationToken stoppingToken)
    {
        try
        {
            irc = new();

            // Get underlying socket
            Socket socket = irc.Client;

            // Set keep-alive
            const bool enableKeepAlive = true;
            const int keepAliveTime = 60000; // Send a packet after a minute of inactivity
            const int keepAliveInterval = 60000; // Send a packet every minute once the keep-alive time has passed

            // Convert the keep-alive settings to a byte array
            byte[] keepAliveValues = new byte[12];
            BitConverter.GetBytes(enableKeepAlive ? 1 : 0).CopyTo(keepAliveValues, 0);
            BitConverter.GetBytes(keepAliveTime).CopyTo(keepAliveValues, 4);
            BitConverter.GetBytes(keepAliveInterval).CopyTo(keepAliveValues, 8);

            // Set the keep-alive settings on the underlying Socket
#pragma warning disable CA1416 // Validate platform compatibility
            socket.IOControl(IOControlCode.KeepAliveValues, keepAliveValues, null);
#pragma warning restore CA1416 // Validate platform compatibility
            await irc.ConnectAsync(_config.ServerAddress, _config.ServerPort);

            using NetworkStream stream = irc.GetStream();
            using StreamReader reader = new(stream);
            using StreamWriter writer = new(stream) { NewLine = "\r\n", AutoFlush = true };

            pingTimer = new Timer(async _ => await SendPingAsync(writer), null, TimeSpan.Zero, TimeSpan.FromMinutes(1));

            currentNick = _config.Nicks.GetNextItemInArray(currentNick);
            await writer.SendRawLineAsync($"NICK {currentNick}");
            await writer.SendRawLineAsync($"USER {_config.Owner} 0 * :{_config.Owner}");

            while (!stoppingToken.IsCancellationRequested && irc.Connected)
            {
                string? inputLine = await reader.ReadLineAsync(stoppingToken);
                if (string.IsNullOrWhiteSpace(inputLine))
                {
                    throw new Exception($"Unexpected Line in inputLine: {inputLine}");
                }

                if (!inputLine.Contains("PING") && !inputLine.Contains("PONG"))
                {
                    BotConsole.WriteLine($"< {inputLine}");  // Print raw IRC data received
                }
                if (inputLine.Contains("PING"))
                {
                    await writer.SendRawLineAsync(inputLine.Replace("PING", "PONG"));
                    continue;
                }

                if (_reconnecting)
                {
                    EnablePlugins(writer);
                    _reconnecting = false;
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
                                await writer.SendRawLineAsync($"PRIVMSG NickServ :IDENTIFY {_config.NickPass}");
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
                                if (_channelsToJoin.All(x => x.Status == "joined"))
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
                                foreach (KeyValuePair<string, Func<string, IEnumerable<string>>> trigger in triggers)
                                {
                                    try
                                    {
                                        if (!userMessage.Contains(trigger.Key, StringComparison.OrdinalIgnoreCase)) continue;
                                        foreach (string answer in trigger.Value(userMessage))
                                        {
                                            await writer.SayToChannel(channel, answer);
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        BotConsole.WriteErrorLine(e.Message);
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
            Environment.Exit(0);
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
        finally
        {
            pingTimer?.Dispose();
            _reconnecting = true;
        }
    }

    private void EnablePlugins(StreamWriter writer)
    {
        BotConsole.WriteSystemLine("Reloading Plugins");
        _pluginService.LoadPlugins(_config.PluginFolder, _config.Root);
        triggers = _pluginService.GetTriggerWords();
        _timers = _pluginService.GetReportingTimers(writer, _channelsToJoin);
        foreach (System.Timers.Timer timer in _timers)
        {
            timer.Enabled = true;
        }
    }
    private async Task SendPingAsync(StreamWriter writer)
    {
        if (irc?.Connected == true)
        {
            try
            {
                const string pingData = "PING :RoboLlamaPing";
                await writer.WriteLineAsync(pingData);
                await writer.FlushAsync();
            }
            catch
            {
                irc.Close();
            }
        }
    }
}