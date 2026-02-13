using System;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using events = Exiled.Events.Handlers;

namespace Scp079BotIntegration
{
    public class Plugin : Plugin<Config>
    {
        public override string Name => "SCP-079-INTEGRATION";
        public override string Prefix => Name;
        public override string Author => "Morkamo";
        public override Version RequiredExiledVersion => new(9, 12, 0);
        public override Version Version => new(1, 0, 0);

        public static Plugin Instance;

        public override void OnEnabled()
        {
            Instance = this;
            base.OnEnabled();
        }

        public override void OnDisabled() 
        {
            Instance = null;
            base.OnDisabled();
        }
    }
}