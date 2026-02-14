using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using SCP_079_DISCORD_BOT.Components;
using SCP_079_DISCORD_BOT.Components.Enums;

namespace SCP_079_DISCORD_BOT.Commands;

public sealed class GameTimeSlashCommand : ApplicationCommandModule
{
    private const int MaxDays = 90;

    [SlashCommand("gametime", "Показать игровые часы на сервере за последние N дней")]
    public async Task GameTime(
        InteractionContext ctx,

        [Option("server", "Сервер")]
        [Choice("Classic", "classic")]
        [Choice("Modded", "modded")]
        [Choice("OnlyEvents", "onlyevents")]
        string server,

        [Option("days", "Период (1-90 дней)")]
        long days,

        [Option("user", "Пользователь (необязательно)")]
        DiscordUser? user = null
    )
    {
        Utils.BotLog($"[{DateTime.Now}] '{ctx.Member?.DisplayName}' used slash command -> '{nameof(GameTime)}' -> #{ctx.Channel.Name}");

        await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

        try
        {
            var db = Program.Db;
            if (db is null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                    .AddEmbed(new DiscordEmbedBuilder()
                        .WithTitle("Ошибка")
                        .WithDescription("База данных недоступна.")
                        .WithColor(DiscordColor.Red)));

                return;
            }

            var safeDays = (int)Math.Clamp(days, 1, MaxDays);

            var target = user ?? ctx.User;
            var linkedSteam = await db.GetLinkedSteamAsync(target.Id); 

            if (!linkedSteam.HasValue)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                    .AddEmbed(new DiscordEmbedBuilder()
                        .WithTitle("Нет привязки")
                        .WithDescription("У выбранного пользователя нет привязки к Steam.")
                        .WithColor(DiscordColor.Red)));

                return;
            }

            var page = 1;

            var embed = await GameTimeUi.BuildEmbedAsync(
                server,
                safeDays,
                page,
                target,
                linkedSteam.Value
            );

            var builder = new DiscordWebhookBuilder().AddEmbed(embed);

            if (GameTimeUi.TryBuildButtons(server, safeDays, ctx.User.Id, page, embed.Footer?.Text, out var components))
                builder.AddComponents(components);

            await ctx.EditResponseAsync(builder);
        }
        catch (Exception ex)
        {
            Utils.BotLog($"GameTime ERROR: {ex}", LogType.Error);

            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .AddEmbed(new DiscordEmbedBuilder()
                    .WithTitle("Ошибка")
                    .WithDescription("Не удалось получить статистику. Попробуйте позже.")
                    .WithColor(DiscordColor.Red)));
        }
    }
}
