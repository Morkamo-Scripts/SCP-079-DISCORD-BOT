using System.Text.Json;
using System.Threading;
using System.Windows.Forms;
using DSharpPlus;
using SCP_079_DISCORD_BOT.Components;
using SCP_079_DISCORD_BOT.Components.Enums;

namespace SCP_079_DISCORD_BOT;

public class Program
{
    public static readonly string Author = "Morkamo";
    public static readonly string Version = "0.0.1";
    public static readonly BuildType BuildType = BuildType.Debug;
    
    [STAThread]
    private static void Main()
    {
        const string mutexName = "SCP-079-DISCORD-BOT-SINGLETON";
        using var mutex = new Mutex(true, mutexName, out bool isNew);

        if (!isNew)
        {
            ConsoleWindow.Show();
            Utils.BotLog("SCP-079 is already running in system tray!", LogType.Error);
            Thread.Sleep(5000);
            return;
        }

        Console.Title = "SCP-079";

        var config = LoadConfig();

        if (string.IsNullOrWhiteSpace(config.BotSettings.Token))
        {
            Utils.BotLog("Bot token is missing!", LogType.Error);
            Thread.Sleep(5000);
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (config.ProgramLaunch.StartInSystemTray)
            ConsoleWindow.Hide();

        Application.Run(new TrayApplicationContext(config));
    }

    private static Config LoadConfig()
    {
        const string path = "config.json";

        if (!File.Exists(path))
        {
            var defaultConfig = new Config();

            var json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(path, json);

            Console.WriteLine("config.json has been created! Replace default bot token in config file!");
            Environment.Exit(0);
        }

        var content = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Config>(content) ?? new Config();
    }
}
