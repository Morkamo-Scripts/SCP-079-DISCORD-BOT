using System.Reflection;
using DSharpPlus;
using SCP_079_DISCORD_BOT.Components.Enums;

namespace SCP_079_DISCORD_BOT.Components;

public sealed class TrayApplicationContext : ApplicationContext
{
    private bool _isOpened = true;
    private readonly NotifyIcon _notifyIcon;

    public TrayApplicationContext(Config config)
    {
        var asm = Assembly.GetExecutingAssembly();
        var resource = "SCP_079_DISCORD_BOT.Assets.Scp079BotLoggoRounded.ico";

        using var stream = asm.GetManifestResourceStream(resource);
        var icon = stream != null ? new Icon(stream) : SystemIcons.Application;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Show", null, (_, _) => ConsoleWindow.Show());
        menu.Items.Add("Hide", null, (_, _) => ConsoleWindow.Hide());
        menu.Items.Add("Exit", null, (_, _) => ExitAsync());

        _notifyIcon = new NotifyIcon
        {
            Text = "SCP-079",
            Icon = icon,
            Visible = true,
            ContextMenuStrip = menu
        };

        
        _notifyIcon.DoubleClick += (_, _) =>
        {
            if (_isOpened)
                ConsoleWindow.Hide();
            else
                ConsoleWindow.Show();

            _isOpened = !_isOpened;
        };
        
        _ = StartBotAsync(config);
    }

    private async Task StartBotAsync(Config config)
    {
        try
        {
            var discord = new DiscordClient(new DiscordConfiguration
            {
                Token = config.BotSettings.Token,
                TokenType = TokenType.Bot,
                Intents = DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents
            });

            discord.Ready += (_, _) =>
            {
                Utils.BotLog(
                    $"[{Program.BuildType}] SCP-079 v{Program.Version} by {Program.Author} has been started!",
                    LogType.Complete
                );
                
                Utils.BotLog(
                    $"[PS] For only femboy users!",
                    LogType.Warning
                );
                
                return Task.CompletedTask;
            };

            await discord.ConnectAsync();
        }
        catch (Exception e)
        {
            Utils.BotLog(e.ToString(), LogType.Error);
        }
    }

    private async void ExitAsync()
    {
        ConsoleWindow.Show();
        
        Utils.BotLog("SCP-079 is shutdowning...", ConsoleColor.Magenta);
        await Task.Delay(2000);
        Utils.BotLog("GoodBye!", ConsoleColor.Cyan);
        await Task.Delay(2000);

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        ExitThread();
    }
}
