using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using SCP_079_DISCORD_BOT.Commands;
using SCP_079_DISCORD_BOT.Components.Enums;

namespace SCP_079_DISCORD_BOT.Components;

public static class GameTimePager
{
    public static async Task OnComponentAsync(DiscordClient client, ComponentInteractionCreateEventArgs e)
    {
        if (e.Id is null || !e.Id.StartsWith("gt:", StringComparison.Ordinal))
            return;

        try
        {
            var parts = e.Id.Split(':');
            if (parts.Length != 5)
            {
                await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
                return;
            }

            var server = parts[1];
            if (!int.TryParse(parts[2], out var days))
            {
                await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
                return;
            }

            if (!ulong.TryParse(parts[3], out var targetDiscordId))
            {
                await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
                return;
            }

            if (!int.TryParse(parts[4], out var page))
            {
                await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
                return;
            }

            var requesterId = targetDiscordId;

            if (e.User.Id != requesterId)
            {
                await e.Interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .AddEmbed(new DiscordEmbedBuilder()
                            .WithTitle("Недоступно")
                            .WithDescription("Эта панель принадлежит другому пользователю.")
                            .WithColor(DiscordColor.Red))
                        .AsEphemeral(true));

                return;
            }

            var db = Program.Db;
            if (db is null)
            {
                await e.Interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .AddEmbed(new DiscordEmbedBuilder()
                            .WithTitle("Ошибка")
                            .WithDescription("База данных недоступна.")
                            .WithColor(DiscordColor.Red))
                        .AsEphemeral(true));

                return;
            }

            await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);

            var linkedSteam = await db.GetLinkedSteamAsync(targetDiscordId);
            if (!linkedSteam.HasValue)
            {
                await e.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder()
                    .AddEmbed(new DiscordEmbedBuilder()
                        .WithTitle("Нет привязки")
                        .WithDescription("У вас нет привязки Steam к Discord.")
                        .WithColor(DiscordColor.Red)));

                return;
            }

            var embed = await GameTimeUi.BuildEmbedAsync(
                server,
                days,
                page,
                e.User,
                linkedSteam.Value
            );

            var builder = new DiscordWebhookBuilder().AddEmbed(embed);

            if (GameTimeUi.TryBuildButtons(server, days, requesterId, page, embed.Footer?.Text, out var components))
                builder.AddComponents(components);

            await e.Interaction.EditOriginalResponseAsync(builder);
        }
        catch (Exception ex)
        {
            Utils.BotLog($"GameTimePager ERROR: {ex}", LogType.Error);

            try
            {
                await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
            }
            catch
            {
            }
        }
    }
}
