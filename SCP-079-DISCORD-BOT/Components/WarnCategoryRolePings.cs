using DSharpPlus.Entities;
using SCP_079_DISCORD_BOT.Components.Enums;

namespace SCP_079_DISCORD_BOT.Components;

public static class WarnCategoryRolePings
{
    public static readonly Dictionary<WarnCategory, string> CategoryToRole = new()
    {
        { WarnCategory.Discord, "1266397434204917871" },
        { WarnCategory.Discord_Admin, "1266397434204917871" },
        { WarnCategory.Classic_Donate, "1268387628890198048" },
        { WarnCategory.Classic_Admin, "1268387628890198048" },
        { WarnCategory.OnlyEvents_Admin, "1268387628890198048" },
        { WarnCategory.OnlyEvents_Donate, "1268387628890198048" }
    };

    public static string? BuildPing(DiscordGuild guild, WarnCategory category)
    {
        if (!CategoryToRole.TryGetValue(category, out var raw))
            return null;

        raw = (raw ?? string.Empty).Trim();
        if (raw.Length == 0)
            return null;

        var tokens = raw.Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
            return null;

        var mentions = new List<string>(tokens.Length);

        foreach (var token in tokens)
        {
            if (TryResolveRoleMention(guild, token, out var mention))
                mentions.Add(mention);
        }

        return mentions.Count == 0 ? null : string.Join(" ", mentions);
    }

    private static bool TryResolveRoleMention(DiscordGuild guild, string token, out string mention)
    {
        mention = null!;

        token = (token ?? string.Empty).Trim();
        if (token.Length == 0)
            return false;

        if (token.StartsWith("<@&", StringComparison.Ordinal) && token.EndsWith(">", StringComparison.Ordinal))
        {
            var idPart = token.Substring(3, token.Length - 4);
            if (ulong.TryParse(idPart, out var roleId))
            {
                mention = $"<@&{roleId}>";
                return true;
            }
        }

        if (token.StartsWith("@", StringComparison.Ordinal))
            token = token.Substring(1).Trim();

        if (ulong.TryParse(token, out var byId))
        {
            mention = $"<@&{byId}>";
            return true;
        }

        var role = guild.Roles.Values.FirstOrDefault(r => string.Equals(r.Name, token, StringComparison.OrdinalIgnoreCase));
        if (role == null)
            return false;

        mention = $"<@&{role.Id}>";
        return true;
    }
}
