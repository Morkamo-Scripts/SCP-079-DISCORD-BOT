using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using SCP_079_DISCORD_BOT.Components;

namespace SCP_079_DISCORD_BOT.Commands;

public class BasicPrefixCommands : BaseCommandModule
{
    [Command("ping")]
    public async Task Ping(CommandContext ctx)
    {
        Utils.BotLog($"[{DateTime.Now}] '{ctx.Member?.DisplayName}' used prefix command -> " +
                     $"'{nameof(Ping)}' -> #{ctx.Channel.Name}");
        await ctx.RespondAsync("Pong!");
    }
}