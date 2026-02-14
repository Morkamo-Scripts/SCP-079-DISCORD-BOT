using System;
using System.Net.Http;
using System.Text;
using CommandSystem;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;

namespace Scp079BotIntegration.Commands.ConfirmLink;

[CommandHandler(typeof(ClientCommandHandler))]
[CommandHandler(typeof(RemoteAdminCommandHandler))]
public class ConfirmLinkCommand : ICommand
{
    public string Command { get; } = "confirmLink";
    public string[] Aliases { get; } = { "clink" };
    public string Description { get; } = "Привязывает ваш аккаунт Steam к Discord по уникальному коду!";
    
    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (arguments.Count < 1)
        {
            response = "<color=orange>Формат: clink [code]. Пример: clink F73KJ1.</color>";
            return false;
        }

        var player = Player.Get(sender);
        if (player == null)
        {
            response = "<color=red>Команду можно использовать только из игры.</color>";
            return false;
        }

        var code = (arguments.At(0) ?? string.Empty).Trim().ToUpperInvariant();
        if (code.Length != 6)
        {
            response = "<color=red>Код должен быть из 6 символов.</color>";
            return false;
        }

        var steamId64 = ExtractSteamId64(player);
        if (string.IsNullOrWhiteSpace(steamId64))
        {
            response = "<color=red>Не удалось определить ваш SteamID64.</color>";
            return false;
        }

        var cfg = InstanceConfig();

        if (string.IsNullOrWhiteSpace(cfg.ApiBaseUrl))
        {
            response = "<color=red>API BaseUrl не настроен в конфиге сервера.</color>";
            return false;
        }

        if (string.IsNullOrWhiteSpace(cfg.ApiSecret))
        {
            response = "<color=red>API Secret не настроен в конфиге сервера.</color>";
            return false;
        }

        try
        {
            Log.Info($"ConfirmLink -> API={cfg.ApiBaseUrl}, Code={code}, Steam={steamId64}");

            var result = ConfirmViaApi(cfg.ApiBaseUrl, cfg.ApiSecret, cfg.ApiTimeoutSeconds, code, steamId64);

            Log.Info($"ConfirmLink -> Result={result}");

            switch (result)
            {
                case ConfirmApiResult.Success:
                    response = "<color=green>Ваш аккаунт Steam успешно привязан к вашему Discord!</color>";
                    return true;

                case ConfirmApiResult.Expired:
                    response = "<color=red>Код истек. Запросите новый через /linkSteam в Discord.</color>";
                    return false;

                case ConfirmApiResult.Invalid:
                    response = "<color=red>Код недействителен. Проверьте ввод или запросите новый через /linkSteam в Discord.</color>";
                    return false;

                case ConfirmApiResult.Mismatch:
                    response = "<color=red>SteamID не совпадает с тем, который был указан при запросе.</color>";
                    return false;

                case ConfirmApiResult.Unauthorized:
                    response = "<color=red>Сервер подтверждения отклонил запрос (неверный секрет).</color>";
                    return false;

                case ConfirmApiResult.Unreachable:
                    response = "<color=red>Сервер подтверждения недоступен. Попробуйте позже.</color>";
                    return false;

                default:
                    response = "<color=red>Неизвестная ошибка подтверждения.</color>";
                    return false;
            }
        }
        catch (Exception ex)
        {
            Log.Error($"ConfirmLink failed: {ex}");
            response = "<color=red>Ошибка при подтверждении. Попробуйте позже.</color>";
            return false;
        }
    }

    private static string ExtractSteamId64(Player player)
    {
        var userId = player.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return string.Empty;

        var at = userId.IndexOf('@');
        var raw = at >= 0 ? userId.Substring(0, at) : userId;

        return ulong.TryParse(raw, out _) ? raw : string.Empty;
    }
    
    private static ConfirmApiResult ConfirmViaApi(string baseUrl, string secret, int timeoutSeconds, string code, string steamId64)
    {
        var url = baseUrl.TrimEnd('/') + "/api/confirm-link";

        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds));

        var payload =
            "{" +
            $"\"secret\":\"{EscapeJson(secret)}\"," +
            $"\"code\":\"{EscapeJson(code)}\"," +
            $"\"steamId\":\"{EscapeJson(steamId64)}\"" +
            "}";

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        HttpResponseMessage resp;
        try
        {
            resp = client.PostAsync(url, content).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log.Warn($"ConfirmLink -> HTTP unreachable: {ex.Message}");
            return ConfirmApiResult.Unreachable;
        }

        Log.Info($"ConfirmLink -> HTTP status: {(int)resp.StatusCode}");

        if ((int)resp.StatusCode == 401)
            return ConfirmApiResult.Unauthorized;

        string body;
        try
        {
            body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty;
        }
        catch
        {
            body = string.Empty;
        }

        if (resp.IsSuccessStatusCode)
            return ConfirmApiResult.Success;

        if (body.IndexOf("expired", StringComparison.OrdinalIgnoreCase) >= 0)
            return ConfirmApiResult.Expired;

        if (body.IndexOf("steam_mismatch", StringComparison.OrdinalIgnoreCase) >= 0)
            return ConfirmApiResult.Mismatch;

        return ConfirmApiResult.Invalid;
    }

    private static string EscapeJson(string s)
    {
        return s
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
    }

    private static Config InstanceConfig()
    {
        return Plugin.Instance.Config;
    }

    private enum ConfirmApiResult
    {
        Success,
        Expired,
        Invalid,
        Mismatch,
        Unauthorized,
        Unreachable
    }
}