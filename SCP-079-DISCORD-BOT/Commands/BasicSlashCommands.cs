using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
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
        string reason)
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
                     $"подумать о своём поведении.", LogType.Complete);

        await ctx.CreateResponseAsync(
            $"Пользователь **@{target.DisplayName}** заглушен!\n" +
            $"Длительность: **{days} д. {hours} ч. {mins} м.**"
        );
    }
}