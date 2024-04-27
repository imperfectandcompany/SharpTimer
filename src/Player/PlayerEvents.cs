using System.Drawing;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;

namespace SharpTimer
{
    public partial class SharpTimer
    {
        private void OnPlayerConnect(CCSPlayerController? player, bool isForBot = false)
        {
            try
            {
                if (player == null)
                {
                    SharpTimerError("Player object is null.");
                    return;
                }

                if (player.PlayerPawn == null)
                {
                    SharpTimerError("PlayerPawn is null.");
                    return;
                }

                if (player.PlayerPawn.Value!.MovementServices == null)
                {
                    SharpTimerError("MovementServices is null.");
                    return;
                }

                int playerSlot = player.Slot;
                string steamID = player.SteamID.ToString();
                string playerName = player.PlayerName;

                try
                {
                    connectedPlayers[playerSlot] = new CCSPlayerController(player.Handle);

                    PlayerTimerInfo playerTimer = new()
                    {
                        MovementService = new CCSPlayer_MovementServices(player.PlayerPawn.Value.MovementServices!.Handle),
                        StageTimes = [],
                        StageVelos = [],
                        CurrentMapStage = 0,
                        CurrentMapCheckpoint = 0,
                        IsRecordingReplay = false,
                        SetRespawnPos = null,
                        SetRespawnAng = null
                    };
                    if (AdminManager.PlayerHasPermissions(player, "@css/root")) playerTimer.ZoneToolWire = [];
                    playerTimers[playerSlot] = playerTimer;

                    if (jumpStatsEnabled) playerJumpStats[playerSlot] = new PlayerJumpStats();
                    if (enableReplays) playerReplays[playerSlot] = new PlayerReplays();

                    if (isForBot == false) _ = Task.Run(async () => await IsPlayerATester(steamID, playerSlot));

                    //PlayerSettings
                    if (useMySQL == true && isForBot == false) _ = Task.Run(async () => await GetPlayerStats(player, steamID, playerName, playerSlot, true));

                    if (connectMsgEnabled == true && useMySQL == false) Server.PrintToChatAll($"{msgPrefix}Player {ChatColors.Red}{playerName} {ChatColors.White}connected!");
                    if (cmdJoinMsgEnabled == true && isForBot == false) PrintAllEnabledCommands(player);

                    SharpTimerDebug($"Added player {playerName} with Slot {playerSlot} to connectedPlayers");
                    SharpTimerDebug($"Total players connected: {connectedPlayers.Count}");
                    SharpTimerDebug($"Total playerTimers: {playerTimers.Count}");
                    SharpTimerDebug($"Total playerReplays: {playerReplays.Count}");

                    if (isForBot == true || hideAllPlayers == true)
                    {
                        player.PlayerPawn.Value.Render = Color.FromArgb(0, 0, 0, 0);
                        Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseModelEntity", "m_clrRender");
                    }
                    else if (removeLegsEnabled == true)
                    {
                        player.PlayerPawn.Value.Render = Color.FromArgb(254, 254, 254, 254);
                        Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseModelEntity", "m_clrRender");
                    }
                }
                finally
                {
                    if (connectedPlayers[playerSlot] == null)
                    {
                        connectedPlayers.Remove(playerSlot);
                    }

                    if (playerTimers[playerSlot] == null)
                    {
                        playerTimers.Remove(playerSlot);
                    }
                }
            }
            catch (Exception ex)
            {
                SharpTimerError($"Error in OnPlayerConnect: {ex.Message}");
            }
        }

        private void OnPlayerDisconnect(CCSPlayerController? player, bool isForBot = false)
        {
            if (player == null) return;

            try
            {
                var playerSlot = player.Slot;
                
                if (isForBot == true && connectedReplayBots.TryGetValue(playerSlot, out var connectedReplayBot))
                {
                    connectedReplayBots.Remove(playerSlot);
                    SharpTimerDebug($"Removed bot {connectedReplayBot.PlayerName} with UserID {connectedReplayBot.UserId} from connectedReplayBots.");
                }

                if (connectedPlayers.TryGetValue(playerSlot, out var connectedPlayer))
                {
                    connectedPlayers.Remove(playerSlot);

                    playerTimers.Remove(playerSlot);

                    playerCheckpoints.Remove(playerSlot);

                    specTargets.Remove(player.Pawn.Value!.EntityHandle.Index);

                    if (enableReplays)
                        playerReplays.Remove(playerSlot);

                    SharpTimerDebug($"Removed player {connectedPlayer.PlayerName} with UserID {connectedPlayer.UserId} from connectedPlayers.");
                    SharpTimerDebug($"Removed specTarget index {player.Pawn.Value.EntityHandle.Index} from specTargets.");
                    SharpTimerDebug($"Total players connected: {connectedPlayers.Count}");
                    SharpTimerDebug($"Total playerTimers: {playerTimers.Count}");
                    SharpTimerDebug($"Total specTargets: {specTargets.Count}");

                    if (connectMsgEnabled == true && isForBot == false)
                    {
                        Server.PrintToChatAll($"{msgPrefix}Player {ChatColors.Red}{connectedPlayer.PlayerName} {ChatColors.White}disconnected!");
                    }
                }
            }
            catch (Exception ex)
            {
                SharpTimerError($"Error in OnPlayerDisconnect (probably replay bot related lolxd): {ex.Message}");
            }
        }

        private HookResult OnPlayerChatTeam(CCSPlayerController? player, CommandInfo message)
        {
            if (displayChatTags == false) return HookResult.Continue;
            string msg;
            if (player == null || !player.IsValid || player.IsBot || string.IsNullOrEmpty(message.GetArg(1)))
            {
                return HookResult.Handled;
            }
            else
            {
                msg = message.GetArg(1);
            }

            if (msg.Length > 0 &&
            (msg[0] == '!' || msg[0] == '/' || msg[0] == '.'))
            {
                return HookResult.Continue;
            }
            else
            {
                char rankColor = GetRankColorForChat(player);

                if (playerTimers.TryGetValue(player.Slot, out PlayerTimerInfo? value))
                {
                    Server.PrintToChatAll($" {primaryChatColor}● {(value.IsVip ? $"{ChatColors.Magenta}[{customVIPTag}] " : "")}{rankColor}[{value.CachedRank}]{ChatColors.Default} {player.PlayerName}: {msg}");
                }
                return HookResult.Handled;
            }
        }

        private HookResult OnPlayerChatAll(CCSPlayerController? player, CommandInfo message)
        {
            if (displayChatTags == false) return HookResult.Continue;
            string msg;
            if (player == null || !player.IsValid || player.IsBot || string.IsNullOrEmpty(message.GetArg(1)))
            {
                return HookResult.Handled;
            }
            else
            {
                msg = message.GetArg(1);
            }

            if (msg.Length > 0 &&
            (msg[0] == '!' || msg[0] == '/' || msg[0] == '.'))
            {
                return HookResult.Continue;
            }
            else
            {
                char rankColor = GetRankColorForChat(player);

                if (playerTimers.TryGetValue(player.Slot, out PlayerTimerInfo? value))
                {
                    Server.PrintToChatAll($" {ChatColors.Grey}[ALL] {primaryChatColor}● {(value.IsVip ? $"{ChatColors.Magenta}[{customVIPTag}] " : "")}{rankColor}[{value.CachedRank}]{ChatColors.Default} {player.PlayerName}: {msg}");
                }
                return HookResult.Handled;
            }
        }
    }
}