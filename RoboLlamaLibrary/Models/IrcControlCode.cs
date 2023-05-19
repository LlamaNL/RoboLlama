namespace RoboLlamaLibrary.Models;

public static class IrcControlCode
{
    public const char Action = (char)0x01;
    public const char Bold = (char)0x02;
    public const char Color = (char)0x03;
    public const char Italic = (char)0x09;
    public const char StrikeThrough = (char)0x13;
    public const char Reset = (char)0x0f;
    public const char Underline = (char)0x15;
    public const char Underline2 = (char)0x1f;
    public const char Reverse = (char)0x16;
}