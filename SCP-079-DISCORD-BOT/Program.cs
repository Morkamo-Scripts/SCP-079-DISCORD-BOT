using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using SCP_079_DISCORD_BOT.Components;
using SCP_079_DISCORD_BOT.Components.Enums;

namespace SCP_079_DISCORD_BOT;

public class Program
{
    public static readonly string Author = "Morkamo";
    public static readonly string Version = "0.0.2";
    public static readonly BuildType BuildType = BuildType.Debug;
    
    public static Config? Config;
    
    [DllImport("kernel32.dll")]
    private static extern bool SetConsoleOutputCP(uint wCodePageId);

    [DllImport("kernel32.dll")]
    private static extern bool SetConsoleCP(uint wCodePageId);
    
    [STAThread]
    private static void Main()
    {
        SetConsoleOutputCP(65001);
        SetConsoleCP(65001);

        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;
        
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

        Config = LoadConfig();

        if (string.IsNullOrWhiteSpace(Config.BotSettings.Token))
        {
            Utils.BotLog("Bot token is missing!", LogType.Error);
            Thread.Sleep(5000);
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (Config.ProgramLaunch.StartInSystemTray)
            ConsoleWindow.Hide();

        Application.Run(new TrayApplicationContext(Config));
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
