using SCP_079_DISCORD_BOT.Components.Enums;

namespace SCP_079_DISCORD_BOT.Components;

public static class Utils
{
    public static void BotLog(string message, LogType logType = LogType.Info)
    {
        ConsoleColor foregroundColor;
        string messagePrefix;
        switch (logType)
        {
            case LogType.Info:
                foregroundColor = ConsoleColor.DarkCyan;
                messagePrefix = "SCP-079";
                break;
            case LogType.Warning:
                foregroundColor = ConsoleColor.Yellow;
                messagePrefix = "Warning";
                break;
            case LogType.Error:
                foregroundColor = ConsoleColor.Red;
                messagePrefix = "Error";
                break;
            case LogType.Complete:
                foregroundColor = ConsoleColor.Green;
                messagePrefix = "Info";
                break;
            default:
                foregroundColor = ConsoleColor.White;
                messagePrefix = "SCP-079";
                break;
        }

        Console.ForegroundColor = foregroundColor;
        Console.WriteLine($"[{messagePrefix}] {message}");
    }
    
    public static void BotLog(string message, ConsoleColor messageColor, LogType logType = LogType.Custom)
    {
        Console.ForegroundColor = messageColor;
        Console.WriteLine($"[{logType}] {message}");
    }
}