using Exiled.API.Interfaces;

namespace Scp079BotIntegration
{
    public sealed class Config : IConfig
    {
        public bool IsEnabled { get; set; } = true;
        public bool Debug { get; set; } = false;

        public string ServerIdenifier { get; set; } = "Classic";
        
        public string ApiBaseUrl { get; set; } = "http://127.0.0.1:5005";
        public string ApiSecret { get; set; } = "SUPER_LONG_RANDOM_SECRET_STRING";
        public int ApiTimeoutSeconds { get; set; } = 5;
    }
}