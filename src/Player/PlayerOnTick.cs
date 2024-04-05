using System.Drawing;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;

namespace SharpTimer
{
    public partial class SharpTimer
    {
        public void PlayerOnTick()
        {
            try
            {
                var updates = new Dictionary<int, PlayerTimerInfo>();

                foreach (CCSPlayerController player in connectedPlayers.Values)
                {
                    if (player == null || !player.IsValid || player.Slot == null) continue;

                    if ((CsTeam)player.TeamNum == CsTeam.Spectator)
                    {
                        SpectatorOnTick(player);
                        continue;
                    }

                    if (playerTimers[player.Slot].IsAddingStartZone || playerTimers[player.Slot].IsAddingEndZone)
                    {
                        OnTickZoneTool(player);
                        continue;
                    }

                    if (playerTimers.TryGetValue(player.Slot, out PlayerTimerInfo playerTimer) && IsAllowedPlayer(player))
                    {
                        if (!IsAllowedPlayer(player))
                        {
                            playerTimer.IsTimerRunning = false;
                            playerTimer.TimerTicks = 0;
                            playerCheckpoints.Remove(player.Slot);
                            playerTimer.TicksSinceLastCmd++;
                            continue;
                        }

                        bool isTimerRunning = playerTimer.IsTimerRunning;
                        int timerTicks = playerTimer.TimerTicks;
                        PlayerButtons? playerButtons = player.Buttons;
                        Vector playerSpeed = player.PlayerPawn.Value.AbsVelocity;

                        string formattedPlayerVel = Math.Round(use2DSpeed ? playerSpeed.Length2D()
                                                                            : playerSpeed.Length())
                                                                            .ToString("0000");
                        string formattedPlayerPre = Math.Round(ParseVector(playerTimer.PreSpeed ?? "0 0 0").Length2D()).ToString("000");
                        string playerTime = FormatTime(timerTicks);
                        string playerBonusTime = FormatTime(playerTimer.BonusTimerTicks);
                        string timerLine = playerTimer.IsBonusTimerRunning
                                            ? $" <font color='gray' class='fontSize-s horizontal-center'>Bonus: {playerTimer.BonusStage}</font> <i><font class='fontSize-l horizontal-center' color='{primaryHUDcolor}'>{playerBonusTime}</font></i> <br>"
                                            : isTimerRunning
                                                ? $" <font color='gray' class='fontSize-s horizontal-center'>{GetPlayerPlacement(player)}</font> <i><font class='fontSize-l horizontal-center' color='{primaryHUDcolor}'>{playerTime}</font></i>{((playerTimer.CurrentMapStage != 0 && useStageTriggers == true) ? $"<font color='gray' class='fontSize-s horizontal-center'> {playerTimer.CurrentMapStage}/{stageTriggerCount}</font>" : "")} <br>"
                                                : playerTimer.IsReplaying
                                                    ? $" <font class='horizontal-center' color='red'>◉ REPLAY {FormatTime(playerReplays[player.Slot].CurrentPlaybackFrame)}</font> <br>"
                                                    : "";

                        string veloLine = $" {(playerTimer.IsTester ? playerTimer.TesterSparkleGif : "")}<font class='fontSize-s horizontal-center' color='{tertiaryHUDcolor}'>Speed:</font> <i>{(playerTimer.IsReplaying ? "<font class=''" : "<font class='fontSize-l horizontal-center'")} color='{secondaryHUDcolor}'>{formattedPlayerVel}</font></i> <font class='fontSize-s horizontal-center' color='gray'>({formattedPlayerPre})</font>{(playerTimer.IsTester ? playerTimer.TesterSparkleGif : "")} <br>";
                        string infoLine = !playerTimer.IsReplaying
                                            ? $"<font class='fontSize-s horizontal-center' color='gray'>{playerTimer.CachedPB} " + $"({playerTimer.CachedMapPlacement}) | </font>" + $"{playerTimer.RankHUDIcon} <font class='fontSize-s horizontal-center' color='gray'>" +
                                              $"{(currentMapTier != null ? $" | Tier: {currentMapTier}" : "")}" +
                                              $"{(currentMapType != null ? $" | {currentMapType}" : "")}" +
                                              $"{((currentMapType == null && currentMapTier == null) ? $" | {currentMapName} " : "")}  "
                                            : $" <font class='fontSize-s horizontal-center' color='gray'>{playerTimers[player.Slot].ReplayHUDString}</font>";

                        string keysLineNoHtml = $"{((playerButtons & PlayerButtons.Moveleft) != 0 ? "A" : "_")} " +
                                                $"{((playerButtons & PlayerButtons.Forward) != 0 ? "W" : "_")} " +
                                                $"{((playerButtons & PlayerButtons.Moveright) != 0 ? "D" : "_")} " +
                                                $"{((playerButtons & PlayerButtons.Back) != 0 ? "S" : "_")} " +
                                                $"{((playerButtons & PlayerButtons.Jump) != 0 || playerTimer.MovementService.OldJumpPressed ? "J" : "_")} " +
                                                $"{((playerButtons & PlayerButtons.Duck) != 0 ? "C" : "_")}";

                        if (playerTimer.MovementService.OldJumpPressed == true) playerTimer.MovementService.OldJumpPressed = false;

                        string hudContent = timerLine +
                                            veloLine +
                                            infoLine +
                                            ((playerTimer.IsTester && !isTimerRunning && !playerTimer.IsBonusTimerRunning && !playerTimer.IsReplaying) ? playerTimer.TesterPausedGif : "") +
                                            ((playerTimer.IsVip && !playerTimer.IsTester && !isTimerRunning && !playerTimer.IsBonusTimerRunning && !playerTimer.IsReplaying) ? $"<br><img src='https://files.catbox.moe/{playerTimer.VipPausedGif}.gif'><br>" : "") +
                                            ((playerTimer.IsReplaying && playerTimer.VipReplayGif != "x") ? $"<br><img src='https://files.catbox.moe/{playerTimer.VipReplayGif}.gif'><br>" : "");

                        updates[player.Slot] = playerTimer;

                        if (playerTimer.HideTimerHud != true && hudOverlayEnabled == true)
                        {
                            player.PrintToCenterHtml(hudContent);
                        }

                        if (playerTimer.HideKeys != true && playerTimer.IsReplaying != true && keysOverlayEnabled == true)
                        {
                            player.PrintToCenter(keysLineNoHtml);
                        }

                        if (isTimerRunning)
                        {
                            playerTimer.TimerTicks++;
                        }
                        else if (playerTimer.IsBonusTimerRunning)
                        {
                            playerTimer.BonusTimerTicks++;
                        }

                        if (useTriggers == false && playerTimer.IsTimerBlocked == false)
                        {
                            CheckPlayerCoords(player, playerSpeed);
                        }

                        if (triggerPushFixEnabled == true)
                        {
                            CheckPlayerTriggerPushCoords(player);
                        }

                        if (jumpStatsEnabled == true) OnJumpStatTick(player, playerSpeed, player.Pawn?.Value.CBodyComponent?.SceneNode.AbsOrigin, player.PlayerPawn?.Value.EyeAngles, playerButtons);

                        if (forcePlayerSpeedEnabled == true) ForcePlayerSpeed(player, player.Pawn.Value.WeaponServices.ActiveWeapon.Value.DesignerName);

                        if (playerTimer.IsRankPbCached == false)
                        {
                            SharpTimerDebug($"{player.PlayerName} has rank and pb null... calling handler");
                            _ = RankCommandHandler(player, player.SteamID.ToString(), player.Slot, player.PlayerName, true);

                            playerTimer.IsRankPbCached = true;
                        }

                        if (hideAllPlayers == true)
                        {
                            foreach (var gun in player.PlayerPawn.Value.WeaponServices.MyWeapons)
                            {
                                if (gun.Value.Render != Color.FromArgb(0, 255, 255, 255) ||
                                    gun.Value.ShadowStrength != 0.0f)
                                {
                                    gun.Value.Render = Color.FromArgb(0, 255, 255, 255);
                                    gun.Value.ShadowStrength = 0.0f;
                                }
                            }
                        }

                        if (displayScoreboardTags == true &&
                            playerTimer.TicksSinceLastRankUpdate > 511 &&
                            playerTimer.CachedRank != null &&
                            (player.Clan != null || !player.Clan.Contains($"[{playerTimer.CachedRank}]")))
                        {
                            AddScoreboardTagToPlayer(player, playerTimer.CachedRank);
                            playerTimer.TicksSinceLastRankUpdate = 0;
                            SharpTimerDebug($"Setting Scoreboard Tag for {player.PlayerName} from TimerOnTick");
                        }

                        if (playerTimer.IsSpecTargetCached == false || specTargets.ContainsKey(player.Pawn.Value.EntityHandle.Index) == false)
                        {
                            specTargets[player.Pawn.Value.EntityHandle.Index] = new CCSPlayerController(player.Handle);
                            playerTimer.IsSpecTargetCached = true;
                            SharpTimerDebug($"{player.PlayerName} was not in specTargets, adding...");
                        }

                        if (removeCollisionEnabled == true)
                        {
                            if (player.PlayerPawn.Value.Collision.CollisionGroup != (byte)CollisionGroup.COLLISION_GROUP_DISSOLVING || player.PlayerPawn.Value.Collision.CollisionAttribute.CollisionGroup != (byte)CollisionGroup.COLLISION_GROUP_DISSOLVING)
                            {
                                SharpTimerDebug($"{player.PlayerName} has wrong collision group... RemovePlayerCollision");
                                RemovePlayerCollision(player);
                            }
                        }

                        if (playerTimer.MovementService != null && removeCrouchFatigueEnabled == true)
                        {
                            if (playerTimer.MovementService.DuckSpeed != 7.0f)
                            {
                                playerTimer.MovementService.DuckSpeed = 7.0f;
                            }
                        }

                        if (hideAllPlayers == true && player.PlayerPawn.Value.Render != Color.FromArgb(0, 0, 0, 0))
                        {
                            player.PlayerPawn.Value.Render = Color.FromArgb(0, 0, 0, 0);
                            Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseModelEntity", "m_clrRender");
                        }

                        if (((PlayerFlags)player.Pawn.Value.Flags & PlayerFlags.FL_ONGROUND) != PlayerFlags.FL_ONGROUND)
                        {
                            playerTimer.TicksInAir++;
                            if (playerTimer.TicksInAir == 1)
                            {
                                playerTimer.PreSpeed = $"{playerSpeed.X} {playerSpeed.Y} {playerSpeed.Z}";
                            }
                        }
                        else
                        {
                            playerTimer.TicksInAir = 0;
                        }

                        if (enableReplays && !playerTimer.IsReplaying && timerTicks > 0 && playerTimer.IsRecordingReplay && !playerTimer.IsTimerBlocked) ReplayUpdate(player, timerTicks);
                        if (enableReplays && playerTimer.IsReplaying && !playerTimer.IsRecordingReplay && playerTimer.IsTimerBlocked)
                        {
                            ReplayPlay(player);
                        }
                        else
                        {
                            if (!playerTimer.IsTimerBlocked && (player.PlayerPawn.Value.MoveType == MoveType_t.MOVETYPE_OBSERVER || player.PlayerPawn.Value.ActualMoveType == MoveType_t.MOVETYPE_OBSERVER)) SetMoveType(player, MoveType_t.MOVETYPE_WALK);
                        }

                        if (playerTimer.TicksSinceLastCmd < cmdCooldown) playerTimer.TicksSinceLastCmd++;
                        if (playerTimer.TicksSinceLastRankUpdate < 511) playerTimer.TicksSinceLastRankUpdate++;

                        playerButtons = null;
                        formattedPlayerVel = null;
                        formattedPlayerPre = null;
                        playerTime = null;
                        playerBonusTime = null;
                        keysLineNoHtml = null;
                        hudContent = null;
                    }
                }

                foreach (var update in updates)
                {
                    playerTimers[update.Key] = update.Value;
                }
            }
            catch (Exception ex)
            {
                if (ex.Message != "Invalid game event") SharpTimerError($"Error in TimerOnTick: {ex.Message}");
            }
        }

        public void SpectatorOnTick(CCSPlayerController player)
        {
            if (!IsAllowedSpectator(player)) return;

            try
            {
                var target = specTargets[player.Pawn.Value.ObserverServices.ObserverTarget.Index];
                if (playerTimers.TryGetValue(target.Slot, out PlayerTimerInfo playerTimer) && IsAllowedPlayer(target))
                {
                    bool isTimerRunning = playerTimer.IsTimerRunning;
                    int timerTicks = playerTimer.TimerTicks;
                    PlayerButtons? playerButtons;
                    Vector targetSpeed = target.PlayerPawn.Value.AbsVelocity;
                    if (playerTimer.IsReplaying && playerReplays[target.Slot].replayFrames.Count > 0 &&
                        playerReplays[target.Slot].CurrentPlaybackFrame >= 0 &&
                        playerReplays[target.Slot].CurrentPlaybackFrame < playerReplays[target.Slot].replayFrames.Count)
                    {
                        playerButtons = playerReplays[target.Slot].replayFrames[playerReplays[target.Slot].CurrentPlaybackFrame].Buttons;
                    }
                    else
                    {
                        playerButtons = target.Buttons;
                    }

                    string formattedPlayerVel = Math.Round(use2DSpeed ? targetSpeed.Length2D()
                                                                        : targetSpeed.Length())
                                                                        .ToString("0000");
                    string formattedPlayerPre = Math.Round(ParseVector(playerTimer.PreSpeed ?? "0 0 0").Length2D()).ToString("000");
                    string playerTime = FormatTime(timerTicks);
                    string playerBonusTime = FormatTime(playerTimer.BonusTimerTicks);
                    string timerLine = playerTimer.IsBonusTimerRunning
                                        ? $" <font color='gray' class='fontSize-s'>Bonus: {playerTimer.BonusStage}</font> <font class='fontSize-l' color='{primaryHUDcolor}'>{playerBonusTime}</font> <br>"
                                        : isTimerRunning
                                            ? $" <font color='gray' class='fontSize-s'>{GetPlayerPlacement(target)}</font> <font class='fontSize-l' color='{primaryHUDcolor}'>{playerTime}</font>{((playerTimer.CurrentMapStage != 0 && useStageTriggers == true) ? $"<font color='gray' class='fontSize-s'> {playerTimer.CurrentMapStage}/{stageTriggerCount}</font>" : "")} <br>"
                                            : playerTimer.IsReplaying
                                                ? $" <font class='' color='red'>◉ REPLAY {FormatTime(playerReplays[target.Slot].CurrentPlaybackFrame)}</font> <br>"
                                                : "";

                    string veloLine = $" {(playerTimer.IsTester ? playerTimer.TesterSparkleGif : "")}<font class='fontSize-s' color='{tertiaryHUDcolor}'>Speed:</font> <font class='' color='{secondaryHUDcolor}'>{formattedPlayerVel}</font> <font class='fontSize-s' color='gray'>({formattedPlayerPre})</font>{(playerTimer.IsTester ? playerTimer.TesterSparkleGif : "")} <br>";
                    string infoLine = !playerTimer.IsReplaying
                                        ? $"<font class='fontSize-s' color='gray'>{playerTimer.CachedPB} " + $"{playerTimer.CachedMapPlacement} | </font>" + $"{playerTimer.RankHUDIcon} <font class='fontSize-s' color='gray'>" +
                                          $"{(currentMapTier != null ? $" | Tier: {currentMapTier}" : "")}" +
                                          $"{(currentMapType != null ? $" | {currentMapType}" : "")}" +
                                          $"{((currentMapType == null && currentMapTier == null) ? $" {currentMapName} " : "")} </font> <br>"
                                        : $" <font class='fontSize-s' color='gray'>{playerTimers[target.Slot].ReplayHUDString}</font> <br>";

                    string keysLine = $"<font class='fontSize-l' color='{secondaryHUDcolor}'>{((playerButtons & PlayerButtons.Moveleft) != 0 ? "A" : "_")} " +
                                            $"{((playerButtons & PlayerButtons.Forward) != 0 ? "W" : "_")} " +
                                            $"{((playerButtons & PlayerButtons.Moveright) != 0 ? "D" : "_")} " +
                                            $"{((playerButtons & PlayerButtons.Back) != 0 ? "S" : "_")} " +
                                            $"{((playerButtons & PlayerButtons.Jump) != 0 || playerTimer.MovementService.OldJumpPressed ? "J" : "_")} " +
                                            $"{((playerButtons & PlayerButtons.Duck) != 0 ? "C" : "_")}</font>";

                    string hudContent = timerLine +
                                        veloLine +
                                        infoLine +
                                        (keysOverlayEnabled == true ? keysLine : "") +
                                        ((playerTimer.IsTester && !isTimerRunning && !playerTimer.IsBonusTimerRunning && !playerTimer.IsReplaying) ? playerTimer.TesterPausedGif : "") +
                                        ((playerTimer.IsVip && !playerTimer.IsTester && !isTimerRunning && !playerTimer.IsBonusTimerRunning && !playerTimer.IsReplaying) ? $"<br><img src='https://files.catbox.moe/{playerTimer.VipPausedGif}.gif'><br>" : "");

                    if (playerTimer.HideTimerHud != true && hudOverlayEnabled == true)
                    {
                        player.PrintToCenterHtml(hudContent);
                    }

                    playerButtons = null;
                    formattedPlayerVel = null;
                    formattedPlayerPre = null;
                    playerTime = null;
                    playerBonusTime = null;
                    keysLine = null;
                    hudContent = null;
                }
            }
            catch (Exception ex)
            {
                if (ex.Message != "Invalid game event") SharpTimerError($"Error in SpectatorOnTick: {ex.Message}");
            }
        }
    }
}