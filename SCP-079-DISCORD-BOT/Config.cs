namespace SCP_079_DISCORD_BOT;

public sealed class Config
{
    public ProgramSettings ProgramSettings { get; set; } = new();
    public BotSettings BotSettings { get; set; } = new();
}

public class ProgramSettings
{
    public bool StartInSystemTray { get; set; } = false;
    public string ConnectionString { get; set; } = String.Empty;
    
    public bool ApiEnabled { get; set; } = true;
    public string ApiHost { get; set; } = "0.0.0.0";
    public int ApiPort { get; set; } = 5005;
    public string ApiSecret { get; set; } = "CHANGE_ME_TO_RANDOM_SECRET";

}

public class BotSettings
{
    public string Token { get; set; } = String.Empty;
    public string CommandPrefix { get; set; } = "?";
    public ulong ServerId { get; set; } = 0;
    
    public Channels Channels { get; set; } = new();
}

public class Channels
{
    public ulong WarnRequestChannelId { get; set; } = 0;
    public ulong WarnHandlerResponseChannelId { get; set; } = 0;
    public ulong PunishmentReportChannelId { get; set; } = 0;
}