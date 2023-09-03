namespace RoboLlama.Infrastructure;

public static class Extensions
{
    public static string GetNextItemInArray(this string[] arr, string? current = null)
    {
        if (current is null)
        {
            return arr[0];
        }
        int index = Array.IndexOf(arr, current);
        return (arr.Length - 1 == index) ? arr[0] : arr[index + 1];
    }

    public static async Task SendRawLineAsync(this StreamWriter stream, string line)
    {
        await stream.WriteLineAsync(line);
        BotConsole.WriteLine($"> {line}");
    }

    public static async Task SayToChannel(this StreamWriter stream, string channel, string line)
    {
        string message = $"PRIVMSG {channel} :{line}";
        await SendRawLineAsync(stream, message);
    }

    public static Dictionary<string, string?> ToDictionary(this IConfigurationSection section) => section
        .GetChildren()
        .ToDictionary(x => x.Key, x => x.Value);
}
