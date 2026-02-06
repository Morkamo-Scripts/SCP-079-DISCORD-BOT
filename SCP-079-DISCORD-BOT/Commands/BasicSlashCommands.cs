using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.SlashCommands;
using SCP_079_DISCORD_BOT.Components;

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
}