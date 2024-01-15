using RoboLlamaLibrary.Models;
using System.Globalization;
using System.Text;

namespace RoboLlamaLibrary.Infrastructure;

public static class Extensions
{
    public static string ColorFormat(this string text, string frontcolor, string backcolor)
    {
        var sb = new StringBuilder();
        sb.Append(IrcControlCode.Color);
        sb.Append(frontcolor);
        if (!string.IsNullOrEmpty(backcolor))
        {
            sb.Append(',');
            sb.Append(backcolor);
        }
        sb.Append(text);
        sb.Append(IrcControlCode.Color);
        return sb.ToString();
    }

    public static string ToMyFormat(this TimeSpan ts)
    {
        return ts.TotalMinutes < 60 ? ts.ToString(@"mm\:ss", CultureInfo.CurrentCulture) : ts.ToString(@"hh\:mm\:ss", CultureInfo.CurrentCulture);
    }
}
