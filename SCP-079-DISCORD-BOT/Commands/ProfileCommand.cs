/*using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using SCP_079_DISCORD_BOT.Components;

namespace SCP_079_DISCORD_BOT.Commands;

public class ProfileCommand : ApplicationCommandModule
{
    [SlashCommand("profile", "Выводит профиль игрока на сервере")]
    public async Task Profile(InteractionContext ctx,
        [Option("user", "Пользователь")]
        DiscordUser user)
    {
        var embed = new DiscordEmbedBuilder
        {
            Title = $"Профиль **{ctx.Guild.GetMemberAsync(user.Id).Result.DisplayName}**",
            Description =
                $"**Дата регистрации:** 00.00.0000\n" +
                $"**Активные предупреждения:** 0\n" +
                $"**Всего полученных предупреждений:** 0\n" +
                $"**Аккаунт Steam:** Не привязан\n" +
                $"**Наигранные часы:** Steam не привязан",
            Color = DiscordColor.Orange,
            Footer = new DiscordEmbedBuilder.EmbedFooter
            {
                Text = $"От: {ctx.Member?.DisplayName}"
            },
            Timestamp = DateTimeOffset.Now
        };
        
        Utils.BotLog($"[{DateTime.Now}] '{ctx.Member?.DisplayName}' used slash command -> " +
                     $"'{nameof(Profile)}' -> #{ctx.Channel.Name}");

        await ctx.CreateResponseAsync(embed);
    }
}*/