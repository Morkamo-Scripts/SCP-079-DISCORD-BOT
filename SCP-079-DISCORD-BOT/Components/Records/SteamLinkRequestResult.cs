using SCP_079_DISCORD_BOT.Components.Enums;

namespace SCP_079_DISCORD_BOT.Components.Records;

public sealed record SteamLinkRequestResult(
    SteamLinkRequestResultType Type,
    SteamLinkRequestItem? Request
);