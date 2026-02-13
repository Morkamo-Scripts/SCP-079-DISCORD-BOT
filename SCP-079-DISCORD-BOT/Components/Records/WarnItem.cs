using SCP_079_DISCORD_BOT.Components.Enums;

namespace SCP_079_DISCORD_BOT.Components.Records;

public sealed record WarnItem(
    Guid Id,
    long WarnNo,
    ulong GuildId,
    ulong TargetUserId,
    ulong AuthorUserId,
    ulong? ResponsibleUserId,
    string Reason,
    WarnCategory Category,
    WarnStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? ResolvedAt,
    string? ResolutionComment
);
