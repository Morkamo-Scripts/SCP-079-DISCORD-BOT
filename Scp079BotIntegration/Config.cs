using Exiled.API.Interfaces;

namespace Scp079BotIntegration
{
    public sealed class Config : IConfig
    {
        public bool IsEnabled { get; set; } = true;
        public bool Debug { get; set; } = false;

        public string ServerIdenifier { get; set; } = "Classic";
    }
}