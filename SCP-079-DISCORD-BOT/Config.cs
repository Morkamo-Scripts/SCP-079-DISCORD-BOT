using SCP_079_DISCORD_BOT.Components;

namespace SCP_079_DISCORD_BOT;

public sealed class Config
{
    public ProgramLaunch ProgramLaunch { get; set; } = new();
    public BotSettings BotSettings { get; set; } = new();
}

public class ProgramLaunch
{
    public bool StartInSystemTray { get; set; } = false;
}

public class BotSettings
{
    public string Token { get; set; } = String.Empty;
    public string CommandPrefix { get; set; } = ";";
}