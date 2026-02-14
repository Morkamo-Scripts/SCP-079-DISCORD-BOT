using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;
using SCP_079_DISCORD_BOT.Components;
using SCP_079_DISCORD_BOT.Components.Enums;

namespace SCP_079_DISCORD_BOT.Commands;

public sealed class BasicSlashCommands : ApplicationCommandModule
{
    [SlashCommand("ping", "Тестовая команда коммуникации с ботом")]  
    public async Task Ping(InteractionContext ctx)
    {
        Utils.BotLog($"[{DateTime.Now}] '{ctx.Member?.DisplayName}' used slash command -> " +
                     $"'{nameof(Ping)}' -> #{ctx.Channel.Name}");
        await ctx.CreateResponseAsync("Pong!");
    }
    
    [SlashCommand("timeout", "Заглушает игрока на установленное время")]  
    [SlashRequirePermissions(Permissions.ModerateMembers)]
    public async Task Timeout(InteractionContext ctx, 
        [Option("user", "Пользователь")]
        DiscordUser user,

        [Option("minutes", "Длительность в минутах")]
        long minutes,

        [Option("reason", "Reason")]
        string reason,

        [Option("media1", "Медиа 1 (необязательно)")]
        DiscordAttachment? media1 = null,
        [Option("media2", "Медиа 2 (необязательно)")]
        DiscordAttachment? media2 = null,
        [Option("media3", "Медиа 3 (необязательно)")]
        DiscordAttachment? media3 = null,
        [Option("media4", "Медиа 4 (необязательно)")]
        DiscordAttachment? media4 = null,
        [Option("media5", "Медиа 5 (необязательно)")]
        DiscordAttachment? media5 = null,
        [Option("media6", "Медиа 6 (необязательно)")]
        DiscordAttachment? media6 = null,
        [Option("media7", "Медиа 7 (необязательно)")]
        DiscordAttachment? media7 = null,
        [Option("media8", "Медиа 8 (необязательно)")]
        DiscordAttachment? media8 = null,
        [Option("media9", "Медиа 9 (необязательно)")]
        DiscordAttachment? media9 = null,
        [Option("media10", "Медиа 10 (необязательно)")]
        DiscordAttachment? media10 = null)
    {
        var target = await ctx.Guild.GetMemberAsync(user.Id);
        
        if (target.IsBot)
        {
            await ctx.CreateResponseAsync("Нельзя применить для бота");
            return;
        }
        
        if (target.Permissions.HasPermission(Permissions.Administrator))
        {
            await ctx.CreateResponseAsync("Нельзя применить для администратора");
            return;
        }
        
        if (minutes < 1)
        {
            await ctx.CreateResponseAsync("Длительность не может быть меньше 1 минуты");
            return;
        }
        
        if (minutes > 40320)
        {
            await ctx.CreateResponseAsync("Длительность не может быть больше 28 дней");
            return;
        }

        await target.TimeoutAsync(
            DateTimeOffset.UtcNow.AddMinutes(minutes),
            reason
        );

        var totalMinutes = minutes;

        var days = totalMinutes / 1440;
        totalMinutes %= 1440;

        var hours = totalMinutes / 60;
        var mins = totalMinutes % 60;
        
        Utils.BotLog($"[{DateTime.Now}] '{ctx.Member?.DisplayName}' used slash command -> " +
                     $"'{nameof(Timeout)}' -> #{ctx.Channel.Name}");
        
        Utils.BotLog($"[{DateTime.Now}] Пользователь '{ctx.Member?.DisplayName}' отправил пользователя '{target.DisplayName}' " +
                     $"подумать о своём поведении.", ConsoleColor.DarkYellow, LogType.Info);
        
        var embed = new DiscordEmbedBuilder
        {
            Title = "Применен тайм-аут",
            Description =
                $"Пользователь **{target.DisplayName}** заглушен!\n" +
                $"Длительность: **{days} д. {hours} ч. {mins} м.**\n" +
                $"Причина: **{reason}**",
            Color = DiscordColor.Orange,
            Footer = new DiscordEmbedBuilder.EmbedFooter
            {
                Text = $"От: {ctx.Member?.DisplayName}"
            },
            Timestamp = DateTimeOffset.Now
        };

        await ctx.CreateResponseAsync(embed);

        var urls = CollectAttachmentUrls(media1, media2, media3, media4, media5, media6, media7, media8, media9, media10);
        await TrySendTimeoutReportAsync(ctx, embed, urls);
    }

    private static List<string> CollectAttachmentUrls(params DiscordAttachment?[] media)
    {
        var result = new List<string>(10);

        foreach (var m in media)
        {
            if (m is null)
                continue;

            if (!string.IsNullOrWhiteSpace(m.Url))
                result.Add(m.Url);
        }

        return result;
    }

    private static async Task TrySendTimeoutReportAsync(InteractionContext ctx, DiscordEmbedBuilder reportEmbed, List<string> mediaUrls)
    {
        try
        {
            var channelId = Program.Config?.BotSettings?.Channels?.PunishmentReportChannelId ?? 0;
            if (channelId == 0)
                return;

            var channel = await ctx.Client.GetChannelAsync(channelId);

            await channel.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(reportEmbed));

            if (mediaUrls.Count > 0)
                await channel.SendMessageAsync(string.Join("\n", mediaUrls));
        }
        catch (Exception ex)
        {
            Utils.BotLog($"[Timeout] Report send failed: {ex.Message}", LogType.Error);
        }
    }
    
    [SlashCommand("ban", "Банит пользователя на сервере")]
    [SlashRequirePermissions(Permissions.BanMembers)]
    public async Task Ban(
        InteractionContext ctx,

        [Option("user", "Пользователь")]
        DiscordUser user,

        [Option("reason", "Причина")]
        string reason,

        [Option("media1", "Медиа 1 (необязательно)")]
        DiscordAttachment? media1 = null,
        [Option("media2", "Медиа 2 (необязательно)")]
        DiscordAttachment? media2 = null,
        [Option("media3", "Медиа 3 (необязательно)")]
        DiscordAttachment? media3 = null,
        [Option("media4", "Медиа 4 (необязательно)")]
        DiscordAttachment? media4 = null,
        [Option("media5", "Медиа 5 (необязательно)")]
        DiscordAttachment? media5 = null,
        [Option("media6", "Медиа 6 (необязательно)")]
        DiscordAttachment? media6 = null,
        [Option("media7", "Медиа 7 (необязательно)")]
        DiscordAttachment? media7 = null,
        [Option("media8", "Медиа 8 (необязательно)")]
        DiscordAttachment? media8 = null,
        [Option("media9", "Медиа 9 (необязательно)")]
        DiscordAttachment? media9 = null,
        [Option("media10", "Медиа 10 (необязательно)")]
        DiscordAttachment? media10 = null
    )
    {
        Utils.BotLog($"[{DateTime.Now}] '{ctx.Member?.DisplayName}' used slash command -> '{nameof(Ban)}' -> #{ctx.Channel.Name}");

        DiscordMember? target = null;

        try
        {
            target = await ctx.Guild.GetMemberAsync(user.Id);
        }
        catch
        {
        }

        if (target != null)
        {
            if (target.IsBot)
            {
                await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder()
                    .AddEmbed(new DiscordEmbedBuilder()
                        .WithTitle("Ошибка")
                        .WithDescription("Нельзя применить к боту.")
                        .WithColor(DiscordColor.Red)));
                return;
            }

            if (target.Permissions.HasPermission(Permissions.Administrator))
            {
                await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder()
                    .AddEmbed(new DiscordEmbedBuilder()
                        .WithTitle("Ошибка")
                        .WithDescription("Нельзя применить к администратору.")
                        .WithColor(DiscordColor.Red)));
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(reason))
            reason = "Не указана";

        await ctx.Guild.BanMemberAsync(user.Id, 0, reason);

        var displayName = target?.DisplayName ?? user.Username;
        Utils.BotLog($"[{DateTime.Now}] '{ctx.Member?.DisplayName}' banned '{displayName}'.", ConsoleColor.DarkYellow, LogType.Info);

        var reportEmbed = new DiscordEmbedBuilder()
            .WithTitle($"Пользователь заблокирован")
            .WithColor(DiscordColor.Orange)
            .AddField("1) Пользователь", target != null ? $"{target.Mention} ({target.Username})" : $"{user.Mention} ({user.Username})", false)
            .AddField("2) Причина", reason, false)
            .WithFooter($"От: {ctx.Member?.DisplayName}")
            .WithTimestamp(DateTimeOffset.Now);

        var urls = CollectAttachmentUrls(media1, media2, media3, media4, media5, media6, media7, media8, media9, media10);

        await SendBanReportAsync(ctx, reportEmbed, urls);

        await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder()
            .AddEmbed(new DiscordEmbedBuilder()
                .WithTitle("Готово")
                .WithDescription($"Пользователь {user.Mention} забанен.")
                .WithColor(DiscordColor.Orange)));
    }

    private static async Task SendBanReportAsync(InteractionContext ctx, DiscordEmbedBuilder embed, List<string> urls)
    {
        var channelId = Program.Config?.BotSettings?.Channels?.PunishmentReportChannelId ?? 0;

        if (channelId == 0)
        {
            Utils.BotLog("[Ban] PunishmentReportChannelId is not set!", LogType.Error);
            return;
        }

        DiscordChannel? channel;
        try
        {
            channel = await ctx.Client.GetChannelAsync(channelId);
        }
        catch (Exception ex)
        {
            Utils.BotLog($"[Ban] Can't resolve report channel: {ex.Message}", LogType.Error);
            return;
        }

        await channel.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(embed));

        if (urls.Count > 0)
            await channel.SendMessageAsync(string.Join("\n", urls));
    }
}