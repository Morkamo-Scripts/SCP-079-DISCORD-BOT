using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;
using System;
using System.Linq;
using System.Threading.Tasks;
using SCP_079_DISCORD_BOT.Components;
using SCP_079_DISCORD_BOT.Components.Enums;
using SCP_079_DISCORD_BOT.Database;

namespace SCP_079_DISCORD_BOT.Commands;

public sealed class WarnSlashCommands : ApplicationCommandModule
{
    [SlashCommand("warns", "Показывает активные предупреждения пользователя")]
    public async Task Warns(
        InteractionContext ctx,
        [Option("user", "Пользователь")] DiscordUser user)
    {
        try
        {
            if (!EnsureGuild(ctx, out var error))
            {
                await ctx.CreateResponseAsync(error);
                return;
            }

            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            var db = new DbService(Program.Config!.ProgramSettings.ConnectionString);

            await db.ExpireOutdatedWarnsAsync();

            var warns = await db.GetActiveWarnsAsync(ctx.Guild!.Id, user.Id);

            var (embed, components) = WarnInteractions.BuildWarnsPage(user.Id, user.Username, warns, 0);

            var response = new DiscordWebhookBuilder().AddEmbed(embed);

            if (components.Length > 0)
                response.AddComponents(components);

            await ctx.EditResponseAsync(response);
        }
        catch (Exception ex)
        {
            await RespondExceptionAsync(ctx, ex);
        }
    }

    [SlashCommand("warn", "Создает запрос на выдачу предупреждения")]
    [SlashRequirePermissions(Permissions.ModerateMembers)]
    public async Task Warn(
        InteractionContext ctx,
        [Option("user", "Пользователь")] DiscordUser user,
        [Option("reason", "Причина")] string reason,

        [Option("category", "Категория")]
        [Choice("Discord", "Discord")]
        [Choice("Discord_Admin", "Discord_Admin")]
        [Choice("Classic_Donate", "Classic_Donate")]
        [Choice("Classic_Admin", "Classic_Admin")]
        [Choice("OnlyEvents_Admin", "OnlyEvents_Admin")]
        [Choice("OnlyEvents_Donate", "OnlyEvents_Donate")]
        string category,

        [Option("days", "Срок действия (в днях)")] long days,
        
        [Option("comment", "Комментарий")] string? comment = null,

        [Option("media1", "Вложение 1")] DiscordAttachment? media1 = null,
        [Option("media2", "Вложение 2")] DiscordAttachment? media2 = null,
        [Option("media3", "Вложение 3")] DiscordAttachment? media3 = null,
        [Option("media4", "Вложение 4")] DiscordAttachment? media4 = null,
        [Option("media5", "Вложение 5")] DiscordAttachment? media5 = null,
        [Option("media6", "Вложение 6")] DiscordAttachment? media6 = null,
        [Option("media7", "Вложение 7")] DiscordAttachment? media7 = null,
        [Option("media8", "Вложение 8")] DiscordAttachment? media8 = null,
        [Option("media9", "Вложение 9")] DiscordAttachment? media9 = null,
        [Option("media10", "Вложение 10")] DiscordAttachment? media10 = null
    )
    {
        try
        {
            if (!EnsureGuild(ctx, out var error))
            {
                await ctx.CreateResponseAsync(error);
                return;
            }

            if (days < 1 || days > 3650)
            {
                await ctx.CreateResponseAsync(BuildError("Некорректные данные", "Срок должен быть в диапазоне 1-3650 дней."));
                return;
            }

            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1500)
            {
                await ctx.CreateResponseAsync(BuildError("Некорректные данные", "Причина пустая или слишком длинная."));
                return;
            }

            if (!string.IsNullOrWhiteSpace(comment))
            {
                comment = comment.Trim();
                if (comment.Length > 1500)
                {
                    await ctx.CreateResponseAsync(BuildError("Некорректные данные", "Комментарий слишком длинный."));
                    return;
                }
            }
            else
            {
                comment = null;
            }

            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            var expiresAt = DateTimeOffset.UtcNow.AddDays(days);

            var categoryEnum = Enum.Parse<WarnCategory>(category);

            var db = new DbService(Program.Config!.ProgramSettings.ConnectionString);

            var (warnId, warnNo) = await db.CreateWarnAsync(
                ctx.Guild!.Id,
                user.Id,
                ctx.User.Id,
                reason.Trim(),
                comment,
                categoryEnum,
                expiresAt
            );

            var attachments = new[]
            {
                media1, media2, media3, media4, media5,
                media6, media7, media8, media9, media10
            }
            .Where(a => a != null)
            .Cast<DiscordAttachment>()
            .ToList();

            foreach (var a in attachments)
                await db.AddWarnMediaAsync(warnId, a.Url, a.FileName, GetMediaType(a));

            var requestEmbed = new DiscordEmbedBuilder()
                .WithTitle("Запрос на предупреждение")
                .WithColor(DiscordColor.Orange)
                .WithTimestamp(DateTimeOffset.UtcNow)
                .AddField("ID:", warnNo.ToString(), true)
                .AddField("Пользователь:", $"<@{user.Id}>", true)
                .AddField("Отправитель:", $"<@{ctx.User.Id}>", true)
                .AddField("Категория:", category, true)
                .AddField("Действует до:", $"{expiresAt:yyyy-MM-dd HH:mm} UTC", true)
                .AddField("Статус:", "Ожидает рассмотрения", true)
                .AddField("Причина:", reason.Trim());

            if (!string.IsNullOrWhiteSpace(comment))
                requestEmbed.AddField("Комментарий:", comment, false);

            if (attachments.Count > 0)
            {
                requestEmbed.AddField(
                    "Вложения:",
                    string.Join("\n", attachments.Select((a, i) => $"{i + 1}. {a.FileName}")),
                    false
                );
            }

            var requestChannelId = Program.Config!.BotSettings.Channels.WarnRequestChannelId;
            var requestChannel = ctx.Guild!.GetChannel(requestChannelId);

            if (requestChannel == null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(BuildError("Ошибка конфигурации", "Канал заявок не найден."))
                );
                return;
            }
            var previewUrls = attachments.Count == 0
                ? null
                : string.Join("\n", attachments.Select(ToPreviewUrl));

            var rolePing = WarnCategoryRolePings.BuildPing(ctx.Guild!, categoryEnum);

            var msg = new DiscordMessageBuilder();

            if (!string.IsNullOrWhiteSpace(rolePing))
            {
                msg.WithContent(rolePing);
                msg.WithAllowedMentions(new IMention[] { new RoleMention() });
            }

            msg
                .AddEmbed(requestEmbed)
                .AddComponents(
                    new DiscordButtonComponent(ButtonStyle.Success, $"warn:approve:{warnId}", "Принять"),
                    new DiscordButtonComponent(ButtonStyle.Danger, $"warn:abort:{warnId}", "Отклонить")
                );

            var requestMessage = await requestChannel.SendMessageAsync(msg);

            if (!string.IsNullOrWhiteSpace(previewUrls))
            {
                WarnInteractions.RegisterMediaContent(warnId, previewUrls);

                var mediaMessage = await requestChannel.SendMessageAsync(
                    new DiscordMessageBuilder().WithContent(previewUrls)
                );

                WarnInteractions.RegisterMediaMessage(warnId, mediaMessage.Id);
                WarnInteractions.RegisterMediaMessage(requestMessage.Id, mediaMessage.Id);
            }

            var resultEmbed = new DiscordEmbedBuilder()
                .WithTitle("Запрос отправлен")
                .WithColor(DiscordColor.SpringGreen)
                .WithTimestamp(DateTimeOffset.UtcNow)
                .AddField("ID:", warnNo.ToString(), true)
                .AddField("Статус:", "Ожидает рассмотрения", true);

            await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(resultEmbed));
        }
        catch (Exception ex)
        {
            await RespondExceptionAsync(ctx, ex);
        }
    }

    [SlashCommand("unwarn", "Снимает предупреждение")]
    [SlashRequirePermissions(Permissions.ModerateMembers)]
    public async Task Unwarn(
        InteractionContext ctx,
        [Option("warnid", "Идентификатор предупреждения")] long warnId)
    {
        try
        {
            if (!EnsureGuild(ctx, out var error))
            {
                await ctx.CreateResponseAsync(error);
                return;
            }

            if (warnId < 1)
            {
                await ctx.CreateResponseAsync(BuildError("Некорректные данные", "ID должен быть положительным числом."));
                return;
            }

            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            var db = new DbService(Program.Config!.ProgramSettings.ConnectionString);

            var warn = await db.GetWarnByNoAsync(ctx.Guild!.Id, (ulong)warnId);
            if (warn == null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(BuildError("Не найдено", "Предупреждение с таким ID не найдено.")));
                return;
            }

            var ok = await db.UnwarnAsync(warn.Id, ctx.User.Id, "Unwarn command");
            if (!ok)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(BuildError("Невозможно выполнить", "Предупреждение уже обработано или не является активным.")));
                return;
            }

            var resultEmbed = new DiscordEmbedBuilder()
                .WithTitle("Предупреждение снято")
                .WithColor(DiscordColor.Red)
                .WithTimestamp(DateTimeOffset.UtcNow)
                .AddField("ID:", warnId.ToString(), true)
                .AddField("Пользователь:", $"<@{warn.TargetUserId}>", true)
                .AddField("Модератор:", $"<@{ctx.User.Id}>", true)
                .AddField("Результат:", "Отменено", true);

            await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(resultEmbed));
        }
        catch (Exception ex)
        {
            await RespondExceptionAsync(ctx, ex);
        }
    }

    private static bool EnsureGuild(InteractionContext ctx, out DiscordInteractionResponseBuilder error)
    {
        if (Program.Config == null)
        {
            error = new DiscordInteractionResponseBuilder().AddEmbed(BuildError("Ошибка конфигурации", "Config is not loaded"));
            return false;
        }

        if (ctx.Guild == null)
        {
            error = new DiscordInteractionResponseBuilder().AddEmbed(BuildError("Недоступно", "Команда доступна только на сервере."));
            return false;
        }

        error = null!;
        return true;
    }

    private static DiscordEmbedBuilder BuildError(string title, string message)
        => new DiscordEmbedBuilder()
            .WithTitle(title)
            .WithDescription(message)
            .WithColor(DiscordColor.Red)
            .WithTimestamp(DateTimeOffset.UtcNow);

    private static async Task RespondExceptionAsync(InteractionContext ctx, Exception ex)
    {
        Utils.BotLog(ex.ToString(), LogType.Error);

        var embed = BuildError("Ошибка", $"{ex.GetType().Name}: {ex.Message}");

        try
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(embed));
        }
        catch
        {
            try
            {
                await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder().AddEmbed(embed));
            }
            catch { }
        }
    }

    private static string GetMediaType(DiscordAttachment a)
    {
        var name = a.FileName.ToLowerInvariant();

        if (name.EndsWith(".png") || name.EndsWith(".jpg") || name.EndsWith(".jpeg") || name.EndsWith(".webp") || name.EndsWith(".gif"))
            return "image";

        if (name.EndsWith(".mp4") || name.EndsWith(".mov") || name.EndsWith(".webm") || name.EndsWith(".mkv"))
            return "video";

        return "file";
    }

    private static string ToPreviewUrl(DiscordAttachment a)
    {
        var url = a.Url;
        var fileName = a.FileName ?? string.Empty;
        var lower = fileName.ToLowerInvariant();

        var isImage = lower.EndsWith(".png") || lower.EndsWith(".jpg") || lower.EndsWith(".jpeg") || lower.EndsWith(".webp") || lower.EndsWith(".gif");
        var isVideo = lower.EndsWith(".mp4") || lower.EndsWith(".mov") || lower.EndsWith(".webm") || lower.EndsWith(".mkv");

        if (isImage || isVideo)
        {
            url = url
                .Replace("https://cdn.discordapp.com/", "https://media.discordapp.net/", StringComparison.OrdinalIgnoreCase)
                .Replace("http://cdn.discordapp.com/", "https://media.discordapp.net/", StringComparison.OrdinalIgnoreCase);
        }

        var hasQuery = url.Contains('?', StringComparison.Ordinal);
        var sep = hasQuery ? "&" : "?";

        if (isImage)
        {
            var format = lower.EndsWith(".jpg") || lower.EndsWith(".jpeg") ? "jpg" : lower.EndsWith(".webp") ? "webp" : lower.EndsWith(".gif") ? "gif" : "png";
            if (!url.Contains("format=", StringComparison.OrdinalIgnoreCase))
                url += $"{sep}format={format}&width=1024&height=1024";
        }
        else if (isVideo)
        {
            if (!url.Contains("format=", StringComparison.OrdinalIgnoreCase))
                url += $"{sep}format=mp4";
        }

        return url;
    }
}
