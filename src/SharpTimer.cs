using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Cvars;

namespace SharpTimer
{
    [MinimumApiVersion(190)]
    public partial class SharpTimer : BasePlugin
    {
        public override void Load(bool hotReload)
        {
            SharpTimerDebug("Loading Plugin...");

            defaultServerHostname = ConVar.Find("hostname").StringValue;
            Server.ExecuteCommand($"execifexists SharpTimer/config.cfg");

            gameDir = Server.GameDirectory;
            SharpTimerDebug($"Set gameDir to {gameDir}");

            string recordsFileName = $"SharpTimer/PlayerRecords/";
            playerRecordsPath = Path.Join(gameDir + "/csgo/cfg", recordsFileName);

            string mysqlConfigFileName = "SharpTimer/mysqlConfig.json";
            mySQLpath = Path.Join(gameDir + "/csgo/cfg", mysqlConfigFileName);
            SharpTimerDebug($"Set mySQLpath to {mySQLpath}");

            currentMapName = Server.MapName;

            RegisterListener<Listeners.OnMapStart>(OnMapStartHandler);

            RegisterEventHandler<EventPlayerConnectFull>((@event, info) =>
            {
                if (@event.Userid.IsValid)
                {
                    var player = @event.Userid;

                    if (!player.IsValid || player.IsBot)
                    {
                        return HookResult.Continue;
                    }
                    else
                    {
                        OnPlayerConnect(player);
                        return HookResult.Continue;
                    }
                }
                else
                {
                    return HookResult.Continue;
                }
            });

            RegisterEventHandler<EventPlayerTeam>((@event, info) =>
            {
                if (@event.Userid.IsValid)
                {
                    var bot = @event.Userid;

                    if (bot.IsValid && bot.IsBot)
                    {
                        if (startKickingAllFuckingBotsExceptReplayOneIFuckingHateValveDogshitFuckingCompanySmile)
                        {
                            AddTimer(4.0f, () =>
                            {
                                Server.ExecuteCommand($"kickid {bot.Slot}");
                                SharpTimerDebug($"Kicking unused bot on spawn...");
                            });
                            return HookResult.Continue;
                        }
                    }
                    return HookResult.Continue;
                }
                else
                {
                    return HookResult.Continue;
                }
            });

            RegisterEventHandler<EventRoundStart>((@event, info) =>
            {
                LoadMapData();
                SharpTimerDebug($"Loading MapData on RoundStart...");
                return HookResult.Continue;
            });

            RegisterEventHandler<EventPlayerSpawned>((@event, info) =>
            {
                if (@event.Userid.IsValid)
                {
                    if (@event.Userid == null) return HookResult.Continue;

                    var player = @event.Userid;

                    if (player.IsBot || !player.IsValid || player == null)
                    {
                        return HookResult.Continue;
                    }
                    else
                    {
                        /* if (removeCollisionEnabled == true && player.PlayerPawn != null)
                        {
                            RemovePlayerCollision(player);
                        }

                        specTargets[player.Pawn.Value.EntityHandle.Index] = new CCSPlayerController(player.Handle); */
                        AddTimer(5.0f, () =>
                        {
                            if (useMySQL && player.DesiredFOV != (uint)playerTimers[player.Slot].PlayerFov)
                            {
                                SharpTimerDebug($"{player.PlayerName} has wrong PlayerFov {player.DesiredFOV}... SetFov to {(uint)playerTimers[player.Slot].PlayerFov}");
                                SetFov(player, playerTimers[player.Slot].PlayerFov, true);
                            }
                        });
                        return HookResult.Continue;
                    }
                }
                else
                {
                    return HookResult.Continue;
                }
            });

            RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
            {
                if (@event.Userid.IsValid)
                {
                    var player = @event.Userid;

                    if (player.IsBot || !player.IsValid)
                    {
                        return HookResult.Continue;
                    }
                    else
                    {
                        OnPlayerDisconnect(player);
                        return HookResult.Continue;
                    }
                }
                else
                {
                    return HookResult.Continue;
                }
            });

            RegisterEventHandler<EventPlayerJump>((@event, info) =>
            {
                if (@event.Userid.IsValid)
                {
                    var player = @event.Userid;

                    if (player.IsBot || !player.IsValid)
                    {
                        return HookResult.Continue;
                    }
                    else
                    {
                        if (jumpStatsEnabled == true) OnJumpStatJumped(player);
                        return HookResult.Continue;
                    }
                }
                else
                {
                    return HookResult.Continue;
                }
            });

            RegisterEventHandler<EventPlayerSound>((@event, info) =>
            {
                if (@event.Userid.IsValid)
                {
                    var player = @event.Userid;

                    if (player.IsBot || !player.IsValid)
                    {
                        return HookResult.Continue;
                    }
                    else
                    {
                        if (jumpStatsEnabled == true && @event.Step == true) OnJumpStatSound(player);
                        return HookResult.Continue;
                    }
                }
                else
                {
                    return HookResult.Continue;
                }
            });

            RegisterListener<Listeners.OnTick>(PlayerOnTick);

            HookEntityOutput("trigger_multiple", "OnStartTouch", (CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay) =>
            {
                TriggerMultipleOnStartTouch(output, activator, caller, value);
                return HookResult.Continue;
            });

            HookEntityOutput("trigger_multiple", "OnEndTouch", (CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay) =>
            {
                TriggerMultipleOnEndTouch(output, activator, caller, value);
                return HookResult.Continue;
            });

            HookEntityOutput("trigger_teleport", "OnEndTouch", (CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay) =>
            {
                TriggerTeleportOnEndTouch(output, activator, caller, value);
                return HookResult.Continue;
            });

            HookEntityOutput("trigger_teleport", "OnStartTouch", (CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay) =>
            {
                TriggerTeleportOnStartTouch(output, activator, caller, value);
                return HookResult.Continue;
            });

            AddTimer(1.0f, () =>
            {
                DamageHook();
            });

            AddCommandListener("say", OnPlayerChatAll);
            AddCommandListener("say_team", OnPlayerChatTeam);

            SharpTimerConPrint("Plugin Loaded");
        }

        public override void Unload(bool hotReload)
        {
            DamageUnHook();

            RemoveCommandListener("say", OnPlayerChatAll, HookMode.Pre);
            RemoveCommandListener("say_team", OnPlayerChatTeam, HookMode.Pre);

            SharpTimerConPrint("Plugin Unloaded");
        }
    }
}
