using RoboLlamaLibrary.Models;
using System.Globalization;

namespace RoboLlamaLibrary.Infrastructure;

public static class Extensions
{
    public static string ColorFormat(this string text, string frontcolor, string backcolor)
    {
        return $"{IrcControlCode.Color}{frontcolor},{backcolor}{text}{IrcControlCode.Color}";
    }

    public static string ToMyFormat(this TimeSpan ts)
    {
        return ts.TotalMinutes < 60 ? ts.ToString(@"mm\:ss", CultureInfo.CurrentCulture) : ts.ToString(@"hh\:mm\:ss", CultureInfo.CurrentCulture);
    }
}
