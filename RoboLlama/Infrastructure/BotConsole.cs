namespace RoboLlama.Infrastructure;

public static class BotConsole
{
    public static ILogger<Bot>? Logger { get; set; }

    public static void WriteLine(string message)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static void WriteErrorLine(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Logger?.LogError("{message}", message);
        Console.ResetColor();
    }

    public static void WriteSystemLine(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(message);
        Logger?.LogError("{message}", message);
        Console.ResetColor();
    }
}
