using DSharpPlus;
using DSharpPlus.Entities;

namespace SCP_079_DISCORD_BOT.Components;

public static class GameTimeUi
{
    private const int PageSize = 10;

    public static async Task<DiscordEmbedBuilder> BuildEmbedAsync(string server, int days, int page, DiscordUser target, long steamId64)
    {
        server = server.Trim().ToLowerInvariant();
        
        var db = Program.Db!;
        var dayRows = await db.GetGameTimeDaysAsync(server, steamId64, days);
        var totalAll = await db.GetGameTimeTotalMinutesAsync(server, steamId64, null);
        var totalPeriod = await db.GetGameTimeTotalMinutesAsync(server, steamId64, days);

        var totalPages = Math.Max(1, (int)Math.Ceiling(dayRows.Count / (double)PageSize));
        var safePage = Math.Clamp(page, 1, totalPages);

        var start = (safePage - 1) * PageSize;
        var count = Math.Min(PageSize, Math.Max(0, dayRows.Count - start));
        var slice = count > 0 ? dayRows.Skip(start).Take(count).ToList() : new List<Database.DbService.GameTimeDay>();

        var titleServer = server switch
        {
            "classic" => "Classic",
            "modded" => "Modded",
            "onlyevents" => "OnlyEvents",
            _ => server
        };

        var embed = new DiscordEmbedBuilder()
            .WithTitle($"Игровые часы - {titleServer}")
            .WithColor(DiscordColor.Orange);

        embed.AddField("Игрок", target.Username, true);
        embed.AddField("SteamID64", $"{steamId64}", true);
        embed.AddField("Период", $"{days} дн.", true);

        embed.AddField("Итого за период", FormatMinutes(totalPeriod), true);
        embed.AddField("Итого на сервере", FormatMinutes(totalAll), true);

        if (dayRows.Count == 0)
        {
            embed.AddField("По дням", "Нет данных за выбранный период.", false);
        }
        else
        {
            var lines = new List<string>(slice.Count);

            for (var i = 0; i < slice.Count; i++)
            {
                var idx = start + i + 1;
                var d = slice[i];
                lines.Add($"{idx}) {d.Day:dd.MM.yyyy} - {FormatMinutesShort(d.Minutes)}");
            }

            embed.AddField("По дням", string.Join("\n", lines), false);
        }

        if (totalPages > 1)
            embed.WithFooter($"Страница {safePage}/{totalPages}");

        return embed;
    }

    public static bool TryBuildButtons(string server, int days, ulong requesterId, int page, string? footerText, out IReadOnlyList<DiscordComponent> components)
    {
        components = Array.Empty<DiscordComponent>();

        var totalPages = ParseTotalPages(footerText);
        if (totalPages <= 1)
            return false;

        var prevPage = Math.Max(1, page - 1);
        var nextPage = Math.Min(totalPages, page + 1);

        var prevDisabled = page <= 1;
        var nextDisabled = page >= totalPages;

        var prevId = $"gt:{server}:{days}:{requesterId}:{prevPage}";
        var nextId = $"gt:{server}:{days}:{requesterId}:{nextPage}";

        components = new DiscordComponent[]
        {
            new DiscordButtonComponent(ButtonStyle.Secondary, prevId, "◀", prevDisabled),
            new DiscordButtonComponent(ButtonStyle.Secondary, nextId, "▶", nextDisabled)
        };

        return true;
    }

    private static int ParseTotalPages(string? footerText)
    {
        if (string.IsNullOrWhiteSpace(footerText))
            return 1;

        var slash = footerText.IndexOf('/');
        if (slash < 0)
            return 1;

        var right = footerText.Substring(slash + 1).Trim();
        return int.TryParse(right, out var pages) ? pages : 1;
    }

    private static string FormatMinutes(int minutes)
    {
        if (minutes <= 0)
            return "0 м";

        var h = minutes / 60;
        var m = minutes % 60;

        if (h == 0)
            return $"{m} м";

        if (m == 0)
            return $"{h} ч";

        return $"{h} ч {m} м";
    }

    private static string FormatMinutesShort(int minutes)
    {
        if (minutes <= 0)
            return "0 м";

        var h = minutes / 60;
        var m = minutes % 60;

        if (h > 0 && m > 0)
            return $"{h} ч {m} м";

        if (h > 0)
            return $"{h} ч";

        return $"{m} м";
    }
}
