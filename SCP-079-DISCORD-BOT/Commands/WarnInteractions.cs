using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using SCP_079_DISCORD_BOT.Components;
using SCP_079_DISCORD_BOT.Components.Enums;
using SCP_079_DISCORD_BOT.Database;

namespace SCP_079_DISCORD_BOT.Commands;

public static class WarnInteractions
{
    private static readonly ConcurrentDictionary<Guid, ulong> WarnToMediaMessage = new();
    private static readonly ConcurrentDictionary<ulong, ulong> RequestToMediaMessage = new();
    private static readonly ConcurrentDictionary<Guid, string> WarnMediaContent = new();

    public static void RegisterMediaContent(Guid warnId, string mediaContent)
    {
        if (warnId == Guid.Empty)
            return;

        mediaContent = mediaContent?.Trim();
        if (string.IsNullOrWhiteSpace(mediaContent))
            return;

        WarnMediaContent[warnId] = mediaContent;
    }

    public static void RegisterMediaMessage(Guid warnId, ulong mediaMessageId)
    {
        if (warnId == Guid.Empty || mediaMessageId == 0)
            return;

        WarnToMediaMessage[warnId] = mediaMessageId;
    }

    public static void RegisterMediaMessage(ulong requestMessageId, ulong mediaMessageId)
    {
        if (requestMessageId == 0 || mediaMessageId == 0)
            return;

        RequestToMediaMessage[requestMessageId] = mediaMessageId;
    }

    private static void CleanupMappingsByMediaId(ulong mediaMessageId)
    {
        foreach (var kv in RequestToMediaMessage)
        {
            if (kv.Value == mediaMessageId)
                RequestToMediaMessage.TryRemove(kv.Key, out _);
        }

        foreach (var kv in WarnToMediaMessage)
        {
            if (kv.Value == mediaMessageId)
                WarnToMediaMessage.TryRemove(kv.Key, out _);
        }
    }

    private static bool TryGetRegisteredMediaId(Guid warnId, out ulong mediaMessageId)
    {
        mediaMessageId = 0;

        if (warnId == Guid.Empty)
            return false;

        return WarnToMediaMessage.TryGetValue(warnId, out mediaMessageId);
    }

    private static bool TryGetRegisteredMediaId(ulong requestMessageId, out ulong mediaMessageId)
    {
        mediaMessageId = 0;

        if (requestMessageId == 0)
            return false;

        return RequestToMediaMessage.TryGetValue(requestMessageId, out mediaMessageId);
    }

    private static async Task<string?> TryGetRegisteredMediaContentAsync(DiscordChannel? channel, Guid warnId, ulong requestMessageId)
    {
        if (warnId != Guid.Empty && WarnMediaContent.TryGetValue(warnId, out var stored))
        {
            stored = stored?.Trim();
            if (!string.IsNullOrWhiteSpace(stored))
                return stored;
        }

        if (channel == null)
            return null;

        ulong mediaMessageId = 0;

        if (!TryGetRegisteredMediaId(warnId, out mediaMessageId) && !TryGetRegisteredMediaId(requestMessageId, out mediaMessageId))
            return null;

        if (mediaMessageId == 0)
            return null;

        try
        {
            var msg = await channel.GetMessageAsync(mediaMessageId);
            var content = msg?.Content?.Trim();
            if (!string.IsNullOrWhiteSpace(content) && warnId != Guid.Empty)
                WarnMediaContent[warnId] = content;
            return string.IsNullOrWhiteSpace(content) ? null : content;
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<string> SplitMessageByLines(string content, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(content))
            yield break;

        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var current = string.Empty;

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd('\r').Trim();
            if (line.Length == 0)
                continue;

            if (current.Length == 0)
            {
                if (line.Length <= maxLen)
                {
                    current = line;
                    continue;
                }

                for (var i = 0; i < line.Length; i += maxLen)
                    yield return line.Substring(i, Math.Min(maxLen, line.Length - i));

                continue;
            }

            if (current.Length + 1 + line.Length <= maxLen)
            {
                current += "\n" + line;
                continue;
            }

            yield return current;
            current = string.Empty;

            if (line.Length <= maxLen)
            {
                current = line;
                continue;
            }

            for (var i = 0; i < line.Length; i += maxLen)
                yield return line.Substring(i, Math.Min(maxLen, line.Length - i));
        }

        if (current.Length > 0)
            yield return current;
    }


    private static async Task TryDeleteRegisteredMediaAsync(DiscordChannel? channel, Guid warnId)
    {
        if (channel == null || warnId == Guid.Empty)
            return;

        if (!WarnToMediaMessage.TryRemove(warnId, out var mediaMessageId))
            return;

        CleanupMappingsByMediaId(mediaMessageId);

        try
        {
            var msg = await channel.GetMessageAsync(mediaMessageId);
            await msg.DeleteAsync();
        }
        catch
        {
        }
    }

    private static async Task TryDeleteRegisteredMediaAsync(DiscordChannel? channel, ulong requestMessageId)
    {
        if (channel == null || requestMessageId == 0)
            return;

        if (!RequestToMediaMessage.TryRemove(requestMessageId, out var mediaMessageId))
            return;

        CleanupMappingsByMediaId(mediaMessageId);

        try
        {
            var msg = await channel.GetMessageAsync(mediaMessageId);
            await msg.DeleteAsync();
        }
        catch
        {
        }
    }


    private static void ForgetRegisteredMedia(Guid warnId)
    {
        if (warnId == Guid.Empty)
            return;

        if (!WarnToMediaMessage.TryRemove(warnId, out var mediaMessageId))
            return;

        CleanupMappingsByMediaId(mediaMessageId);
    }

    private static void ForgetRegisteredMedia(ulong requestMessageId)
    {
        if (requestMessageId == 0)
            return;

        if (!RequestToMediaMessage.TryRemove(requestMessageId, out var mediaMessageId))
            return;

        CleanupMappingsByMediaId(mediaMessageId);
    }

    private static void ForgetRegisteredMediaContent(Guid warnId)
    {
        if (warnId == Guid.Empty)
            return;

        WarnMediaContent.TryRemove(warnId, out _);
    }

    private static async Task TrySendWarnDmAsync(
        DiscordClient client,
        DiscordGuild? guild,
        WarnItem? warn,
        ulong moderatorUserId)
    {
        if (warn == null)
            return;

        if (client == null)
            return;

        if (guild == null)
            return;

        try
        {
            var member = await guild.GetMemberAsync(warn.TargetUserId);

            var dm = await member.CreateDmChannelAsync();

            var embed = new DiscordEmbedBuilder()
                .WithTitle("Вы получили предупреждение")
                .WithColor(DiscordColor.Orange)
                .WithTimestamp(DateTimeOffset.UtcNow)
                .AddField("ID:", warn.WarnNo.ToString(), true)
                .AddField("Сервер:", guild.Name, true)
                .AddField("Категория:", warn.Category.ToString(), true)
                .AddField(
                    "Действует до:",
                    warn.ExpiresAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm") + " UTC",
                    true)
                .AddField("Модератор:", $"<@{moderatorUserId}>", true)
                .AddField("Причина:", warn.Reason ?? "-", false);

            if (!string.IsNullOrWhiteSpace(warn.ResolutionComment))
                embed.AddField("Комментарий:", warn.ResolutionComment, false);

            await dm.SendMessageAsync(
                new DiscordMessageBuilder().AddEmbed(embed)
            );
        }
        catch (Exception ex)
        {
            Utils.BotLog(
                $"Warn DM failed to {warn.TargetUserId}: {ex.GetType().Name}: {ex.Message}",
                LogType.Info
            );
        }
    }


    private const int WarnsPageSize = 5;


    public static async Task OnComponentInteractionCreated(DiscordClient client, ComponentInteractionCreateEventArgs e)
    {
        if (Program.Config == null)
            return;

        if (string.IsNullOrWhiteSpace(e.Id))
            return;

        if (e.Id.StartsWith("warns:", StringComparison.OrdinalIgnoreCase))
        {
            await HandleWarnsPaginationAsync(e);
            return;
        }

        if (e.Id.StartsWith("warn:", StringComparison.OrdinalIgnoreCase))
        {
            await HandleWarnDecisionAsync(client, e);
            return;
        }
    }

    public static async Task OnModalSubmitted(DiscordClient client, ModalSubmitEventArgs e)
    {
        if (Program.Config == null)
            return;

        var customId = e.Interaction?.Data?.CustomId;
        if (string.IsNullOrWhiteSpace(customId))
            return;

        if (!customId.StartsWith("warn:abortmodal:", StringComparison.OrdinalIgnoreCase))
            return;

        var parts = customId.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
            return;

        if (!Guid.TryParse(parts[2], out var warnId))
            return;

        var requestChannelId = Program.Config.BotSettings.Channels.WarnRequestChannelId;
        if (requestChannelId != 0 && e.Interaction.Channel != null && e.Interaction.Channel.Id != requestChannelId)
        {
            await e.Interaction.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .AddEmbed(BuildError("Недоступно", "Обработка предупреждений возможна только в канале заявок."))
                    .AsEphemeral(true)
            );
            return;
        }

        var guild = e.Interaction.Guild;
        if (guild == null || e.Interaction.Channel == null)
        {
            await e.Interaction.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .AddEmbed(BuildError("Ошибка", "Сервер или канал недоступны."))
                    .AsEphemeral(true)
            );
            return;
        }

        var member = await guild.GetMemberAsync(e.Interaction.User.Id);
        var perms = member.PermissionsIn(e.Interaction.Channel);

        if (!perms.HasPermission(Permissions.ModerateMembers) && !perms.HasPermission(Permissions.Administrator))
        {
            await e.Interaction.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .AddEmbed(BuildError("Недостаточно прав", "У вас нет прав для обработки предупреждений."))
                    .AsEphemeral(true)
            );
            return;
        }

        await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);

        e.Values.TryGetValue("comment", out var comment);
        comment = comment?.Trim();
        if (string.IsNullOrWhiteSpace(comment))
            comment = null;

        var db = new DbService(Program.Config.ProgramSettings.ConnectionString);
        var ok = await db.ResolveWarnAsync(warnId, WarnStatus.Aborted, e.Interaction.User.Id, comment);

        if (!ok)
        {
            await e.Interaction.EditOriginalResponseAsync(
                new DiscordWebhookBuilder().AddEmbed(BuildError("Ошибка", "Это предупреждение уже было обработано."))
            );
            return;
        }

        var warn = await db.GetWarnByIdAsync(warnId);

        var resolvedEmbed = BuildResolvedEmbed(warnId, WarnStatus.Aborted, e.Interaction.User.Id, warn, comment);
        /*var disabled = BuildDisabledDecisionButtons(warnId);*/

        await e.Interaction.EditOriginalResponseAsync(
            new DiscordWebhookBuilder()
                .AddEmbed(resolvedEmbed)
        );

        var evidenceContent = await TryGetRegisteredMediaContentAsync(e.Interaction.Channel, warnId, 0);

        await TryDeleteRegisteredMediaAsync(e.Interaction.Channel, warnId);

        await SendHandledLogAsync(e.Interaction, warnId, WarnStatus.Aborted, e.Interaction.User.Id, warn, comment, evidenceContent);

        ForgetRegisteredMediaContent(warnId);

        Utils.BotLog($"[{DateTime.Now}] Warn {warnId} resolved to {WarnStatus.Aborted} by {member.DisplayName}", LogType.Info);
    }

    private static async Task HandleWarnDecisionAsync(DiscordClient client, ComponentInteractionCreateEventArgs e)
    {
        var parts = e.Id.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
            return;

        var action = parts[1];
        if (!Guid.TryParse(parts[2], out var warnId))
            return;

        var requestChannelId = Program.Config!.BotSettings.Channels.WarnRequestChannelId;

        if (requestChannelId != 0 && e.Channel.Id != requestChannelId)
        {
            await e.Interaction.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .AddEmbed(BuildError("Недоступно", "Обработка предупреждений возможна только в канале заявок."))
                    .AsEphemeral(true)
            );
            return;
        }

        if (e.Guild == null)
        {
            await e.Interaction.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .AddEmbed(BuildError("Ошибка", "Сервер недоступен."))
                    .AsEphemeral(true)
            );
            return;
        }

        var member = await e.Guild.GetMemberAsync(e.User.Id);
        var perms = member.PermissionsIn(e.Channel);

        if (!perms.HasPermission(Permissions.ModerateMembers) && !perms.HasPermission(Permissions.Administrator))
        {
            await e.Interaction.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .AddEmbed(BuildError("Недостаточно прав", "У вас нет прав для обработки предупреждений."))
                    .AsEphemeral(true)
            );
            return;
        }

        if (action.Equals("abort", StringComparison.OrdinalIgnoreCase))
        {
            var modal = new DiscordInteractionResponseBuilder()
                .WithTitle("Отклонить предупреждение")
                .WithCustomId($"warn:abortmodal:{warnId}")
                .AddComponents(new TextInputComponent(
                    label: "Причина отклонения",
                    customId: "comment",
                    placeholder: "Например: недостаточно доказательств",
                    value: null,
                    required: true,
                    style: TextInputStyle.Paragraph,
                    min_length: 3,
                    max_length: 500
                ));

            await e.Interaction.CreateResponseAsync(InteractionResponseType.Modal, modal);
            return;
        }

        if (!action.Equals("approve", StringComparison.OrdinalIgnoreCase))
            return;

        await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);

        var db = new DbService(Program.Config.ProgramSettings.ConnectionString);
        var ok = await db.ResolveWarnAsync(warnId, WarnStatus.Active, e.User.Id, null);

        if (!ok)
        {
            await e.Interaction.EditOriginalResponseAsync(
                new DiscordWebhookBuilder().AddEmbed(BuildError("Ошибка", "Это предупреждение уже было обработано."))
            );
            return;
        }

        var warn = await db.GetWarnByIdAsync(warnId);

        var resolvedEmbed = BuildResolvedEmbed(warnId, WarnStatus.Active, e.User.Id, warn, null);
        /*var disabled = BuildDisabledDecisionButtons(warnId);*/

        await e.Interaction.EditOriginalResponseAsync(
            new DiscordWebhookBuilder()
                .AddEmbed(resolvedEmbed)
        );

        var requestMessageId = e.Message?.Id ?? 0;
        var evidenceContent = await TryGetRegisteredMediaContentAsync(e.Channel, warnId, requestMessageId);

        await TrySendWarnDmAsync(client, e.Guild, warn, e.User.Id);

        await SendHandledLogAsync(e.Interaction, warnId, WarnStatus.Active, e.User.Id, warn, null, evidenceContent);

        ForgetRegisteredMediaContent(warnId);

        ForgetRegisteredMedia(warnId);
        ForgetRegisteredMedia(requestMessageId);

        Utils.BotLog($"[{DateTime.Now}] Warn {warnId} resolved to {WarnStatus.Active} by {member.DisplayName}", LogType.Info);
    }

    private static async Task HandleWarnsPaginationAsync(ComponentInteractionCreateEventArgs e)
    {
        var parts = e.Id.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
            return;

        if (!ulong.TryParse(parts[1], out var targetUserId))
            return;

        if (!int.TryParse(parts[2], out var page))
            page = 0;

        if (page < 0)
            page = 0;

        await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);

        if (e.Guild == null)
            return;

        var db = new DbService(Program.Config!.ProgramSettings.ConnectionString);
        await db.ExpireOutdatedWarnsAsync();

        var warns = await db.GetActiveWarnsAsync(e.Guild.Id, targetUserId);

        string displayName;
        try
        {
            var member = await e.Guild.GetMemberAsync(targetUserId);
            displayName = member.DisplayName;
        }
        catch
        {
            displayName = targetUserId.ToString();
        }

        var (embed, components) = BuildWarnsPage(targetUserId, displayName, warns, page);

        await e.Interaction.EditOriginalResponseAsync(
            new DiscordWebhookBuilder()
                .AddEmbed(embed)
                .AddComponents(components)
        );
    }

    internal static (DiscordEmbedBuilder embed, DiscordComponent[] components) BuildWarnsPage(
        ulong userId,
        string displayName,
        IReadOnlyList<WarnItem> warns,
        int page)
    {
        if (warns.Count == 0)
        {
            var emptyEmbed = new DiscordEmbedBuilder()
                .WithTitle("Предупреждения")
                .WithDescription($"У пользователя <@{userId}> нет активных предупреждений.")
                .WithColor(DiscordColor.SpringGreen)
                .WithTimestamp(DateTimeOffset.UtcNow);

            return (emptyEmbed, Array.Empty<DiscordComponent>());
        }

        var totalPages = (int)Math.Ceiling(warns.Count / (double)WarnsPageSize);
        page = Math.Clamp(page, 0, totalPages - 1);

        var pageItems = warns
            .Skip(page * WarnsPageSize)
            .Take(WarnsPageSize)
            .ToList();

        var embed = new DiscordEmbedBuilder()
            .WithTitle($"Предупреждения: {displayName}")
            .WithDescription($"Активных предупреждений: **{warns.Count}**\nПользователь: <@{userId}>")
            .WithColor(DiscordColor.Orange)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .WithFooter($"Страница {page + 1} из {totalPages}");

        foreach (var w in pageItems)
        {
            var left = w.ExpiresAt.ToUniversalTime() - DateTimeOffset.UtcNow;

            string leftText;
            if (left <= TimeSpan.Zero)
                leftText = "истекло";
            else if (left < TimeSpan.FromMinutes(1))
                leftText = "меньше минуты";
            else
                leftText = $"{(int)left.TotalDays} д. {left.Hours} ч. {left.Minutes} м.";

            var text = $"Причина: {w.Reason}";
            if (!string.IsNullOrWhiteSpace(w.ResolutionComment))
                text += $"\nКомментарий: {w.ResolutionComment}";
            text += $"\nОсталось: {leftText}";

            embed.AddField(
                $"[ID: {w.WarnNo}] | {w.Category}",
                text,
                false
            );
        }

        if (totalPages <= 1)
            return (embed, Array.Empty<DiscordComponent>());

        var components = new DiscordComponent[]
        {
            new DiscordButtonComponent(ButtonStyle.Secondary, $"warns:{userId}:{page - 1}", "Назад", page <= 0),
            new DiscordButtonComponent(ButtonStyle.Secondary, $"warns:{userId}:{page + 1}", "Вперед", page >= totalPages - 1)
        };

        return (embed, components);
    }

    private static DiscordEmbed BuildResolvedEmbed(Guid warnId, WarnStatus status, ulong responsibleUserId, WarnItem? warn, string? comment)
    {
        var title = status == WarnStatus.Active ? "Предупреждение обработано" : "Запрос на предупреждение обработан";

        var embed = new DiscordEmbedBuilder()
            .WithTitle(title)
            .WithColor(status == WarnStatus.Active ? DiscordColor.SpringGreen : DiscordColor.Red)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .AddField("ID:", warn != null ? warn.WarnNo.ToString() : warnId.ToString(), false)
            .AddField("Результат:", status == WarnStatus.Active ? "Принято" : "Отклонено", true)
            .AddField("Модератор:", $"<@{responsibleUserId}>", true);

        if (warn != null)
        {
            embed
                .AddField("Пользователь:", $"<@{warn.TargetUserId}>", true)
                .AddField("Отправитель:", $"<@{warn.AuthorUserId}>", true)
                .AddField("Категория:", warn.Category.ToString(), true)
                .AddField("Действует до:", $"{warn.ExpiresAt.ToUniversalTime():yyyy-MM-dd HH:mm} UTC", true)
                .AddField("Причина:", warn.Reason, false);

            var commentText = !string.IsNullOrWhiteSpace(warn.ResolutionComment) ? warn.ResolutionComment : comment;
            if (!string.IsNullOrWhiteSpace(commentText))
                embed.AddField("Комментарий:", commentText, false);
        }

        return embed.Build();
    }

    private static DiscordComponent[] BuildDisabledDecisionButtons(Guid warnId)
    {
        return new DiscordComponent[]
        {
            new DiscordButtonComponent(ButtonStyle.Success, $"warn:approve:{warnId}", "Принять", true),
            new DiscordButtonComponent(ButtonStyle.Danger, $"warn:abort:{warnId}", "Отклонить", true)
        };
    }

    private static async Task SendHandledLogAsync(DiscordInteraction interaction, Guid warnId, WarnStatus status, ulong responsibleUserId, WarnItem? warn, string? comment, string? evidenceContent)
    {
        var responseChannelId = Program.Config!.BotSettings.Channels.WarnHandlerResponseChannelId;
        if (responseChannelId == 0 || interaction.Guild == null)
            return;

        var responseChannel = interaction.Guild.GetChannel(responseChannelId);
        if (responseChannel == null)
            return;

        var embed = new DiscordEmbedBuilder()
            .WithTitle("Предупреждение обработано")
            .WithColor(status == WarnStatus.Active ? DiscordColor.SpringGreen : DiscordColor.Red)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .AddField("ID:", warn != null ? warn.WarnNo.ToString() : warnId.ToString(), false)
            .AddField("Результат:", status == WarnStatus.Active ? "Принято" : "Отклонено", true)
            .AddField("Модератор:", $"<@{responsibleUserId}>", true);

        if (warn != null)
        {
            embed
                .AddField("Пользователь:", $"<@{warn.TargetUserId}>", true)
                .AddField("Отправитель:", $"<@{warn.AuthorUserId}>", true)
                .AddField("Категория:", warn.Category.ToString(), true)
                .AddField("Действует до:", $"{warn.ExpiresAt.ToUniversalTime():yyyy-MM-dd HH:mm} UTC", true)
                .AddField("Причина:", warn.Reason, false);

            var commentText = !string.IsNullOrWhiteSpace(warn.ResolutionComment) ? warn.ResolutionComment : comment;
            if (!string.IsNullOrWhiteSpace(commentText))
                embed.AddField("Комментарий:", commentText, false);
        }

        await responseChannel.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(embed));

        if (string.IsNullOrWhiteSpace(evidenceContent))
            return;

        foreach (var chunk in SplitMessageByLines(evidenceContent, 1900))
        {
            await responseChannel.SendMessageAsync(
                new DiscordMessageBuilder()
                    .WithContent(chunk)
                    .WithAllowedMentions(Array.Empty<IMention>())
            );
        }
    }

    private static DiscordEmbedBuilder BuildError(string title, string description)
        => new DiscordEmbedBuilder()
            .WithTitle(title)
            .WithDescription(description)
            .WithColor(DiscordColor.Red)
            .WithTimestamp(DateTimeOffset.UtcNow);
}
