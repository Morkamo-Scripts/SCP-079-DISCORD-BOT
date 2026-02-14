using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using SCP_079_DISCORD_BOT.Components;
using SCP_079_DISCORD_BOT.Components.Enums;
using SCP_079_DISCORD_BOT.Database;

namespace SCP_079_DISCORD_BOT;

public sealed class HttpServer
{
    private readonly Config _config;
    private readonly Func<DbService?> _dbAccessor;

    public HttpServer(Config config, Func<DbService?> dbAccessor)
    {
        _config = config;
        _dbAccessor = dbAccessor;
    }

    public void TryStart()
    {
        var ps = _config.ProgramSettings;

        if (!ps.ApiEnabled)
        {
            Utils.BotLog("HTTP API is disabled by config.");
            return;
        }

        if (string.IsNullOrWhiteSpace(ps.ApiSecret))
        {
            Utils.BotLog("HTTP API secret is missing (ProgramSettings.ApiSecret). API will NOT start.", LogType.Error);
            return;
        }

        var host = string.IsNullOrWhiteSpace(ps.ApiHost) ? "0.0.0.0" : ps.ApiHost;
        var port = ps.ApiPort <= 0 ? 5005 : ps.ApiPort;
        var url = $"http://{host}:{port}";

        _ = Task.Run(async () =>
        {
            try
            {
                var builder = WebApplication.CreateBuilder();
                var app = builder.Build();

                app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

                app.MapPost("/api/confirm-link", async (ConfirmLinkRequest request) =>
                {
                    if (!string.Equals(request.Secret, ps.ApiSecret, StringComparison.Ordinal))
                        return Results.Unauthorized();

                    if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.SteamId))
                        return Results.BadRequest(new { error = "invalid_payload" });

                    var code = request.Code.Trim().ToUpperInvariant();
                    var steamId = request.SteamId.Trim();

                    Utils.BotLog($"HTTP API confirm request: Code={code}, Steam={steamId}");

                    var db = _dbAccessor();
                    if (db is null)
                    {
                        Utils.BotLog("HTTP API confirm failed: DbService is not initialized (503).", LogType.Error);
                        return Results.StatusCode(503);
                    }

                    ConfirmResult result;
                    try
                    {
                        result = await db.ConfirmSteamLinkAsync(code, steamId, TimeSpan.FromMinutes(10));
                    }
                    catch (Exception ex)
                    {
                        Utils.BotLog($"HTTP API confirm exception: {ex}", LogType.Error);
                        return Results.StatusCode(500);
                    }

                    Utils.BotLog($"HTTP API confirm result: {result}");

                    return result switch
                    {
                        ConfirmResult.Success => Results.Ok(new { success = true }),
                        ConfirmResult.Expired => Results.BadRequest(new { error = "expired" }),
                        ConfirmResult.NotFound => Results.BadRequest(new { error = "invalid" }),
                        ConfirmResult.Mismatch => Results.BadRequest(new { error = "steam_mismatch" }),
                        _ => Results.BadRequest(new { error = "unknown" })
                    };
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

    private sealed class ConfirmLinkRequest
    {
        public string Secret { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string SteamId { get; set; } = string.Empty;
    }
}
