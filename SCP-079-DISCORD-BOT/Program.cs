﻿using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Npgsql.NameTranslation;
using SCP_079_DISCORD_BOT.Components;
using SCP_079_DISCORD_BOT.Components.Enums;
using SCP_079_DISCORD_BOT.Database;

namespace SCP_079_DISCORD_BOT;

public class Program
{
    public static readonly string Author = "Morkamo";
    public static readonly string Version = "0.0.3";
    public static readonly BuildType BuildType = BuildType.Debug;

    public static Config? Config;
    public static DbService? Db;

    [DllImport("kernel32.dll")]
    private static extern bool SetConsoleOutputCP(uint wCodePageId);

    [DllImport("kernel32.dll")]
    private static extern bool SetConsoleCP(uint wCodePageId);

    [STAThread]
    [Obsolete("Obsolete")]
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

        try
        {
            var translator = new NpgsqlNullNameTranslator();

            NpgsqlConnection.GlobalTypeMapper.MapEnum<WarnCategory>("warn_category", translator);
            NpgsqlConnection.GlobalTypeMapper.MapEnum<WarnStatus>("warn_status", translator);
        }
        catch (Exception ex)
        {
            Utils.BotLog($"PostgreSQL enum mapping failed: {ex.Message}", LogType.Error);
            Thread.Sleep(5000);
            return;
        }

        TryStartApiHost(Config);

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (Config.ProgramSettings.StartInSystemTray)
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

    private static void TryStartApiHost(Config config)
    {
        var enabled = GetBoolByPath(config, "ProgramSettings", "ApiEnabled") ?? true;
        if (!enabled)
        {
            Utils.BotLog("HTTP API is disabled by config.");
            return;
        }

        var host = GetStringByPath(config, "ProgramSettings", "ApiHost") ?? "0.0.0.0";
        var port = GetIntByPath(config, "ProgramSettings", "ApiPort") ?? 5005;
        var secret = GetStringByPath(config, "ProgramSettings", "ApiSecret");

        if (string.IsNullOrWhiteSpace(secret))
        {
            Utils.BotLog("HTTP API secret is missing (ProgramSettings.ApiSecret). API will NOT start.", LogType.Error);
            return;
        }

        var url = $"http://{host}:{port}";

        _ = Task.Run(async () =>
        {
            try
            {
                var builder = WebApplication.CreateBuilder();
                var app = builder.Build();

                app.MapPost("/api/confirm-link", async (ConfirmLinkRequest request) =>
                {
                    if (!string.Equals(request.Secret, secret, StringComparison.Ordinal))
                        return Results.Unauthorized();

                    if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.SteamId))
                        return Results.BadRequest(new { error = "invalid_payload" });

                    var db = Db;
                    if (db is null)
                        return Results.StatusCode(503);

                    var method = db.GetType().GetMethod("ConfirmSteamLinkAsync", new[] { typeof(string), typeof(string) });
                    if (method is null)
                        return Results.StatusCode(501);

                    object? invokeResult;
                    try
                    {
                        invokeResult = method.Invoke(db, new object[] { request.Code, request.SteamId });
                    }
                    catch (Exception ex)
                    {
                        Utils.BotLog($"API ConfirmSteamLinkAsync invoke failed: {ex}", LogType.Error);
                        return Results.StatusCode(500);
                    }

                    if (invokeResult is Task task)
                    {
                        await task.ConfigureAwait(false);

                        var taskType = task.GetType();
                        if (taskType.IsGenericType && taskType.GetProperty("Result") is { } prop)
                        {
                            var res = prop.GetValue(task);

                            if (res is null)
                                return Results.Ok(new { success = true });

                            var text = res.ToString() ?? string.Empty;

                            return Results.Ok(new { success = true, result = text });
                        }

                        return Results.Ok(new { success = true });
                    }

                    return Results.Ok(new { success = true });
                });

                Utils.BotLog($"HTTP API started: {url}", LogType.Complete);

                await app.RunAsync(url).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Utils.BotLog($"HTTP API host failed: {ex}", LogType.Error);
            }
        });
    }

    private static string? GetStringByPath(object root, params string[] path)
    {
        object? current = root;
        foreach (var name in path)
        {
            if (current is null) return null;
            var t = current.GetType();
            var p = t.GetProperty(name);
            current = p?.GetValue(current);
        }

        return current as string;
    }

    private static bool? GetBoolByPath(object root, params string[] path)
    {
        object? current = root;
        foreach (var name in path)
        {
            if (current is null) return null;
            var t = current.GetType();
            var p = t.GetProperty(name);
            current = p?.GetValue(current);
        }

        return current is bool b ? b : null;
    }

    private static int? GetIntByPath(object root, params string[] path)
    {
        object? current = root;
        foreach (var name in path)
        {
            if (current is null) return null;
            var t = current.GetType();
            var p = t.GetProperty(name);
            current = p?.GetValue(current);
        }

        return current is int i ? i : null;
    }

    private sealed class ConfirmLinkRequest
    {
        public string Secret { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string SteamId { get; set; } = string.Empty;
    }
}
