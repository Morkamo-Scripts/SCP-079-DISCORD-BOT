using System.Reflection;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.SlashCommands;
using Microsoft.Extensions.Logging;
using SCP_079_DISCORD_BOT.Commands;
using SCP_079_DISCORD_BOT.Components.Enums;
using SCP_079_DISCORD_BOT.Database;

namespace SCP_079_DISCORD_BOT.Components;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private DiscordClient? _discord;
    private CommandsNextExtension? _commands;
    private SlashCommandsExtension? _slash;

    private bool _isOpened = true;
    private bool _isExiting = false;

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
            if (string.IsNullOrWhiteSpace(config.ProgramSettings.ConnectionString))
            {
                Utils.BotLog("ConnectionString is missing in config.json (ProgramSettings.ConnectionString)!", LogType.Error);
                await Task.Delay(5000);
                ExitThread();
                return;
            }

            try
            {
                Program.Db = new DbService(config.ProgramSettings.ConnectionString);
                await Program.Db.TestConnectionAsync();
                Utils.BotLog("PostgreSQL connection: OK", LogType.Complete);
            }
            catch (Exception e)
            {
                Utils.BotLog("PostgreSQL connection: FAILED", LogType.Error);
                Utils.BotLog(e.ToString(), LogType.Error);
                await Task.Delay(5000);
                ExitThread();
                return;
            }

            _discord = new DiscordClient(new DiscordConfiguration
            {
                Token = config.BotSettings.Token,
                TokenType = TokenType.Bot,
                Intents = DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents,
                MinimumLogLevel = LogLevel.Error
            });

            _discord.Ready += (_, _) =>
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

            _discord.ComponentInteractionCreated += WarnInteractions.OnComponentInteractionCreated;
            _discord.ModalSubmitted += WarnInteractions.OnModalSubmitted;

            _slash = _discord.UseSlashCommands();

            if (Program.Config?.BotSettings.ServerId == null || Program.Config?.BotSettings.ServerId == 0)
            {
                Utils.BotLog("Bot will not send commands globally. Specify server id (GuildId) in config!", LogType.Error);
                await Task.Delay(5000);
                ExitThread();
                return;
            }

            _slash.RegisterCommands<BasicSlashCommands>(Program.Config?.BotSettings.ServerId);
            _slash.RegisterCommands<WarnSlashCommands>(Program.Config?.BotSettings.ServerId);

            _commands = _discord.UseCommandsNext(new CommandsNextConfiguration
            {
                StringPrefixes = new[] { config.BotSettings.CommandPrefix },
                EnableMentionPrefix = true,
                EnableDms = false
            });
            _commands.RegisterCommands<BasicPrefixCommands>();

            await _discord.ConnectAsync();
        }
        catch (Exception e)
        {
            Utils.BotLog(e.ToString(), LogType.Error);
        }
    }

    private async void ExitAsync()
    {
        if (_isExiting)
            return;

        _isExiting = true;

        ConsoleWindow.Show();

        Utils.BotLog("SCP-079 is shutdowning...", ConsoleColor.Red);
        await Task.Delay(1000);
        Utils.BotLog("GoodBye!", ConsoleColor.Red);

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();

        if (_discord != null)
        {
            await _discord.DisconnectAsync();
        }

        ExitThread();
    }
}
