using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using SCP_079_DISCORD_BOT.Components;
using SCP_079_DISCORD_BOT.Components.Enums;
using SCP_079_DISCORD_BOT.Components.Records;

namespace SCP_079_DISCORD_BOT.Commands;

public class LinkSteamSlashCommand : ApplicationCommandModule
{
    [SlashCommand("linkSteam", "Привязать ваш аккаунт Steam к вашему Discord")]
    public async Task LinkSteam(
        InteractionContext ctx,
        [Option("steamid64", "Ваш SteamID64")] string steamId64)
    {
        Utils.BotLog($"[{DateTime.Now}] '{ctx.Member?.DisplayName}' used slash command -> '{nameof(LinkSteam)}' -> #{ctx.Channel.Name}");

        await ctx.CreateResponseAsync(
            InteractionResponseType.DeferredChannelMessageWithSource,
            new DiscordInteractionResponseBuilder());

        if (!ulong.TryParse(steamId64, out var parsedSteamId))
        {
            await EditEmbedAsync(ctx,
                "Некорректный SteamID",
                "SteamID64 должен состоять только из цифр.",
                DiscordColor.Red);
            return;
        }

        SteamLinkRequestResult result;

        try
        {
            result = await Program.Db!
                .GetOrCreateSteamLinkRequestAsync(
                    ctx.User.Id,
                    (long)parsedSteamId,
                    GenerateSteamLinkCode,
                    TimeSpan.FromMinutes(10));
        }
        catch (Exception ex)
        {
            Utils.BotLog("DB ERROR: " + ex);
    
            await EditEmbedAsync(ctx,
                "Ошибка базы данных",
                "Произошла ошибка при обработке запроса.",
                DiscordColor.Red);
            return;
        }

        if (result.Type == SteamLinkRequestResultType.AlreadyLinked)
        {
            await EditEmbedAsync(ctx,
                "Привязка уже активна",
                "У вас уже есть активная привязка. Если хотите сбросить, используйте /unlinkSteam.",
                DiscordColor.Red);
            return;
        }

        if (result.Type == SteamLinkRequestResultType.SteamAlreadyLinkedToAnotherUser)
        {
            await EditEmbedAsync(ctx,
                "Steam уже привязан",
                "Этот Steam уже привязан к другому Discord аккаунту.",
                DiscordColor.Red);
            return;
        }

        var req = result.Request!;

        var dmText =
            "Код подтверждения привязки Steam:\n" +
            $"`{req.Code}`\n\n" +
            "Зайдите на сервер и подтвердите привязку через команду:\n" +
            $"/confirmLink {req.Code}\n\n" +
            "Код действителен 10 минут.";

        try
        {
            var dm = await ctx.Member!.CreateDmChannelAsync();
            await dm.SendMessageAsync(dmText);

            await EditEmbedAsync(ctx,
                "Запрос получен",
                "Код подтверждения отправлен вам в личные сообщения.\n" +
                "Пожалуйста зайдите на сервер и подтвердите привязку через команду:\n" +
                "`/confirmLink {КОД}`",
                DiscordColor.Orange);
        }
        catch
        {
            await EditEmbedAsync(ctx,
                "Не удалось отправить ЛС",
                "Я не смог отправить вам личное сообщение. Откройте личные сообщения от участников сервера и попробуйте снова.",
                DiscordColor.Red);
        }
    }

    [SlashCommand("unlinkSteam", "Отвязать ваш аккаунт Steam от вашего Discord")]
    public async Task UnlinkSteam(InteractionContext ctx)
    {
        Utils.BotLog($"[{DateTime.Now}] '{ctx.Member?.DisplayName}' used slash command -> '{nameof(UnlinkSteam)}' -> #{ctx.Channel.Name}");

        await ctx.CreateResponseAsync(
            InteractionResponseType.ChannelMessageWithSource,
            new DiscordInteractionResponseBuilder()
                .AddEmbed(new DiscordEmbedBuilder()
                    .WithTitle("В разработке")
                    .WithDescription("Механика отвязки будет добавлена следующим шагом.")
                    .WithColor(DiscordColor.Orange)));
    }

    private static async Task EditEmbedAsync(InteractionContext ctx, string title, string description, DiscordColor color)
    {
        var embed = new DiscordEmbedBuilder()
            .WithTitle(title)
            .WithDescription(description)
            .WithColor(color);

        await ctx.EditResponseAsync(
            new DiscordWebhookBuilder().AddEmbed(embed));
    }

    private static string GenerateSteamLinkCode()
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        Span<char> buffer = stackalloc char[6];

        for (var i = 0; i < buffer.Length; i++)
            buffer[i] = alphabet[Random.Shared.Next(alphabet.Length)];

        return new string(buffer);
    }
}