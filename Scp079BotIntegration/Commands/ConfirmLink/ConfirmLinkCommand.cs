using System;
using CommandSystem;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;

namespace Scp079BotIntegration.Commands.ConfirmLink;

[CommandHandler(typeof(ClientCommandHandler))]
[CommandHandler(typeof(RemoteAdminCommandHandler))]
public class ConfirmLinkCommand : ICommand
{
    public string Command { get; } = "confirmLink";
    public string[] Aliases { get; } = ["clink"];
    public string Description { get; } = "Привязывает ваш аккаунт Steam к Discord по уникальному коду!";
    
    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (arguments.Count < 1)
        {
            response = "<color=orange>Формат ввода: clink [code]. Пример: clink F73KJ1.</color>";
            return false;
        }
        
        
        
        response = "<color=green>Ваш аккаунт Steam успешно привязан к вашему Discord!</color>";
        return true;
    }
    
    private static Config InstanceConfig()
    {
        return Plugin.Instance.Config;
    }
}