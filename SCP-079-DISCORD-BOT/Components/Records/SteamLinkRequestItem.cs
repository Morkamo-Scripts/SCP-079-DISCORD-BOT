namespace SCP_079_DISCORD_BOT.Components.Records;

public sealed record SteamLinkRequestItem(
    ulong DiscordId,
    long SteamId64,
    string Code,
    DateTimeOffset CreatedAt
);