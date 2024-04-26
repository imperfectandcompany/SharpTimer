
using CounterStrikeSharp.API.Core;

namespace SharpTimer
{
    public partial class SharpTimer
    {
        public HookResult TriggerMultipleOnStartTouch(CEntityIOOutput output, CEntityInstance activator, CEntityInstance caller, CVariant value)
        {
            try
            {
                if (activator == null || output == null || value == null || caller == null)
                {
                    SharpTimerDebug("Null reference detected in trigger_multiple OnStartTouch hook.");
                    return HookResult.Continue;
                }

                if (activator.DesignerName != "player" || useTriggers == false) return HookResult.Continue;

                var player = new CCSPlayerController(new CCSPlayerPawn(activator.Handle).Controller.Value!.Handle);

                if (player == null)
                {
                    SharpTimerDebug("Player is null in trigger_multiple OnStartTouch hook.");
                    return HookResult.Continue;
                }

                if (!IsAllowedPlayer(player) || caller.Entity!.Name == null) return HookResult.Continue;

                var callerHandle = caller.Handle;
                var playerSlot = player.Slot;
                var playerName = player.PlayerName;
                var steamID = player.SteamID.ToString();

                /* if (caller.Entity.Name.ToString() == "bhop_block" && IsAllowedPlayer(player) && !playerTimers[player.Slot].IsTimerBlocked && playerTimers[player.Slot].TicksOnBhopBlock > bhopBlockTime)
                {
                    RespawnPlayer(player);
                    return HookResult.Continue;
                } */

                if (useStageTriggers == true && stageTriggers.ContainsKey(callerHandle) && playerTimers[player.Slot].IsTimerBlocked == false && playerTimers[player.Slot].IsTimerRunning == true && IsAllowedPlayer(player))
                {
                    if (stageTriggers[callerHandle] == 1)
                    {
                        playerTimers[player.Slot].CurrentMapStage = 1;
                        return HookResult.Continue;
                    }
                    else
                    {
                        _ = Task.Run(async () => await HandlePlayerStageTimes(player, callerHandle, playerSlot, steamID, playerName));
                        return HookResult.Continue;
                    }
                }

                if (useCheckpointTriggers == true && cpTriggers.ContainsKey(callerHandle) && playerTimers[player.Slot].IsTimerBlocked == false && playerTimers[player.Slot].IsTimerRunning == true && IsAllowedPlayer(player))
                {
                    _ = Task.Run(async () => await HandlePlayerCheckpointTimes(player, callerHandle, playerSlot, steamID, playerName));
                    return HookResult.Continue;
                }

                if (IsValidEndTriggerName(caller.Entity.Name.ToString()) && IsAllowedPlayer(player) && playerTimers[player.Slot].IsTimerRunning && !playerTimers[player.Slot].IsTimerBlocked)
                {
                    OnTimerStop(player);
                    if (enableReplays) OnRecordingStop(player);
                    SharpTimerDebug($"Player {player.PlayerName} entered EndZone");
                    return HookResult.Continue;
                }

                if (IsValidStartTriggerName(caller.Entity.Name.ToString()) && IsAllowedPlayer(player))
                {
                    if (!playerTimers[player.Slot].IsTimerBlocked)
                    {
                        playerCheckpoints.Remove(player.Slot);
                    }

                    InvalidateTimer(player, callerHandle);

                    if ((maxStartingSpeedEnabled == true && use2DSpeed == false && Math.Round(player.PlayerPawn.Value!.AbsVelocity.Length()) > maxStartingSpeed) ||
                        (maxStartingSpeedEnabled == true && use2DSpeed == true && Math.Round(player.PlayerPawn.Value!.AbsVelocity.Length2D()) > maxStartingSpeed))
                    {
                        Action<CCSPlayerController?, float, bool> adjustVelocity = use2DSpeed ? AdjustPlayerVelocity2D : AdjustPlayerVelocity;
                        adjustVelocity(player, maxStartingSpeed, false);
                    }

                    SharpTimerDebug($"Player {player.PlayerName} entered StartZone");

                    return HookResult.Continue;
                }

                var (validEndBonus, endBonusX) = IsValidEndBonusTriggerName(caller.Entity.Name.ToString(), player.Slot);

                if (validEndBonus && IsAllowedPlayer(player) && playerTimers[player.Slot].IsBonusTimerRunning && !playerTimers[player.Slot].IsTimerBlocked)
                {
                    OnBonusTimerStop(player, endBonusX);
                    if (enableReplays) OnRecordingStop(player);
                    SharpTimerDebug($"Player {player.PlayerName} entered Bonus{endBonusX} EndZone");
                    return HookResult.Continue;
                }

                var (validStartBonus, startBonusX) = IsValidStartBonusTriggerName(caller.Entity.Name.ToString());

                if (validStartBonus && IsAllowedPlayer(player))
                {
                    if (!playerTimers[player.Slot].IsTimerBlocked)
                    {
                        playerCheckpoints.Remove(player.Slot);
                    }
                    
                    InvalidateTimer(player, callerHandle);

                    if ((maxStartingSpeedEnabled == true && use2DSpeed == false && Math.Round(player.PlayerPawn.Value!.AbsVelocity.Length()) > maxStartingSpeed) ||
                        (maxStartingSpeedEnabled == true && use2DSpeed == true && Math.Round(player.PlayerPawn.Value!.AbsVelocity.Length2D()) > maxStartingSpeed))
                    {
                        Action<CCSPlayerController?, float, bool> adjustVelocity = use2DSpeed ? AdjustPlayerVelocity2D : AdjustPlayerVelocity;
                        adjustVelocity(player, maxStartingSpeed, false);
                    }
                    SharpTimerDebug($"Player {player.PlayerName} entered Bonus{startBonusX} StartZone");
                    return HookResult.Continue;
                }

                if (IsValidStopTriggerName(caller.Entity.Name.ToString()))
                {
                    InvalidateTimer(player, callerHandle);
                    player.PrintToChat(msgPrefix + $"Timer cancelled due to illegal skip attempt");
                }

                if (IsValidStopTriggerName(caller.Entity.Name.ToString()))
                {
                    InvalidateTimer(player, callerHandle);
                    RespawnPlayer(player);
                    player.PrintToChat(msgPrefix + $"You got reset due to illegal skip attempt");
                }

                return HookResult.Continue;
            }
            catch (Exception ex)
            {
                SharpTimerError($"Exception in trigger_multiple OnStartTouch hook: {ex.Message}");
                return HookResult.Continue;
            }
        }

        public HookResult TriggerMultipleOnEndTouch(CEntityIOOutput oput, CEntityInstance actvtr, CEntityInstance cllr, CVariant vlue)
        {
            var output = oput;
            var activator = actvtr;
            var caller = cllr;
            var value = vlue;

            try
            {
                if (activator == null || output == null || value == null || caller == null)
                {
                    SharpTimerDebug("Null reference detected in trigger_multiple OnEndTouch hook.");
                    return HookResult.Continue;
                }

                if (activator.DesignerName != "player" || useTriggers == false) return HookResult.Continue;

                var player = new CCSPlayerController(new CCSPlayerPawn(activator.Handle).Controller.Value!.Handle);

                if (player == null)
                {
                    SharpTimerDebug("Player is null in trigger_multiple OnEndTouch hook.");
                    return HookResult.Continue;
                }

                if (!IsAllowedPlayer(player) || caller.Entity!.Name == null) return HookResult.Continue;

                /* if (caller.Entity.Name.ToString() == "bhop_block" && IsAllowedPlayer(player) && !playerTimers[player.Slot].IsTimerBlocked)
                {
                    playerTimers[player.Slot].TicksOnBhopBlock = 0;

                    return HookResult.Continue;
                } */

                if (IsValidStartTriggerName(caller.Entity.Name.ToString()) && IsAllowedPlayer(player) && !playerTimers[player.Slot].IsTimerBlocked)
                {
                    OnTimerStart(player);
                    if (enableReplays) OnRecordingStart(player);

                    if (((maxStartingSpeedEnabled == true && use2DSpeed == false && Math.Round(player.PlayerPawn.Value!.AbsVelocity.Length()) > maxStartingSpeed) ||
                        (maxStartingSpeedEnabled == true && use2DSpeed == true && Math.Round(player.PlayerPawn.Value!.AbsVelocity.Length2D()) > maxStartingSpeed)) &&
                        !currentMapOverrideMaxSpeedLimit!.Contains(caller.Entity.Name.ToString()) && currentMapOverrideMaxSpeedLimit != null)
                    {
                        Action<CCSPlayerController?, float, bool> adjustVelocity = use2DSpeed ? AdjustPlayerVelocity2D : AdjustPlayerVelocity;
                        adjustVelocity(player, maxStartingSpeed, false);
                    }

                    SharpTimerDebug($"Player {player.PlayerName} left StartZone");

                    return HookResult.Continue;
                }

                var (validStartBonus, StartBonusX) = IsValidStartBonusTriggerName(caller.Entity.Name.ToString());

                if (validStartBonus == true && IsAllowedPlayer(player) && !playerTimers[player.Slot].IsTimerBlocked)
                {
                    OnTimerStart(player, StartBonusX);
                    if (enableReplays) OnRecordingStart(player, StartBonusX);

                    if (((maxStartingSpeedEnabled == true && use2DSpeed == false && Math.Round(player.PlayerPawn.Value!.AbsVelocity.Length()) > maxStartingSpeed) ||
                        (maxStartingSpeedEnabled == true && use2DSpeed == true && Math.Round(player.PlayerPawn.Value!.AbsVelocity.Length2D()) > maxStartingSpeed)) &&
                        !currentMapOverrideMaxSpeedLimit!.Contains(caller.Entity.Name.ToString()) && currentMapOverrideMaxSpeedLimit != null)
                    {
                        Action<CCSPlayerController?, float, bool> adjustVelocity = use2DSpeed ? AdjustPlayerVelocity2D : AdjustPlayerVelocity;
                        adjustVelocity(player, maxStartingSpeed, false);
                    }

                    SharpTimerDebug($"Player {player.PlayerName} left BonusStartZone {StartBonusX}");

                    return HookResult.Continue;
                }
                return HookResult.Continue;
            }
            catch (Exception ex)
            {
                SharpTimerError($"Exception in trigger_multiple OnEndTouch hook: {ex.Message}");
                return HookResult.Continue;
            }
        }

        public HookResult TriggerTeleportOnStartTouch(CEntityIOOutput oput, CEntityInstance actvtr, CEntityInstance cllr, CVariant vlue)
        {
            var output = oput;
            var activator = actvtr;
            var caller = cllr;
            var value = vlue;

            try
            {
                if (activator == null || output == null || value == null || caller == null)
                {
                    SharpTimerDebug("Null reference detected in trigger_teleport hook.");
                    return HookResult.Continue;
                }

                if (activator.DesignerName != "player")
                {
                    SharpTimerDebug("activator.DesignerName != player in trigger_teleport hook.");
                    return HookResult.Continue;
                }

                var player = new CCSPlayerController(new CCSPlayerPawn(activator.Handle).Controller.Value!.Handle);

                if (player == null)
                {

                    return HookResult.Continue;
                }

                if (!IsAllowedPlayer(player))
                {
                    SharpTimerDebug("Player not allowed in trigger_teleport hook.");
                    return HookResult.Continue;
                }

                if (jumpStatsEnabled) InvalidateJS(player.Slot);

                return HookResult.Continue;
            }
            catch (Exception ex)
            {
                SharpTimerError($"Exception in trigger_teleport hook: {ex.Message}");
                return HookResult.Continue;
            }
        }

        public HookResult TriggerTeleportOnEndTouch(CEntityIOOutput oput, CEntityInstance actvtr, CEntityInstance cllr, CVariant vlue)
        {
            var output = oput;
            var activator = actvtr;
            var caller = cllr;
            var value = vlue;

            try
            {
                if (activator == null || output == null || value == null || caller == null)
                {
                    SharpTimerDebug("Null reference detected in trigger_teleport hook.");
                    return HookResult.Continue;
                }

                if (activator.DesignerName != "player" || resetTriggerTeleportSpeedEnabled == false)
                {
                    return HookResult.Continue;
                }

                var player = new CCSPlayerController(new CCSPlayerPawn(activator.Handle).Controller.Value!.Handle);

                if (player == null)
                {
                    SharpTimerDebug("Player is null in trigger_teleport hook.");
                    return HookResult.Continue;
                }

                if (!IsAllowedPlayer(player)) return HookResult.Continue;

                if (IsAllowedPlayer(player) && resetTriggerTeleportSpeedEnabled) //if (IsAllowedPlayer(player) && resetTriggerTeleportSpeedEnabled && currentMapOverrideDisableTelehop != null)
                {
                    /* string triggerName = caller.Entity.Name.ToString();
                    if (!currentMapOverrideDisableTelehop.Contains(triggerName))
                    {
                        Action<CCSPlayerController?, float, bool> adjustVelocity = use2DSpeed ? AdjustPlayerVelocity2D : AdjustPlayerVelocity;
                        adjustVelocity(player, 0, false);
                    } */
                    if (!currentMapOverrideDisableTelehop)
                    {
                        Action<CCSPlayerController?, float, bool> adjustVelocity = use2DSpeed ? AdjustPlayerVelocity2D : AdjustPlayerVelocity;
                        adjustVelocity(player, 0, false);
                    }
                }

                return HookResult.Continue;
            }
            catch (Exception ex)
            {
                SharpTimerError($"Exception in trigger_teleport hook: {ex.Message}");
                return HookResult.Continue;
            }
        }

        public HookResult TriggerPushOnStartTouch(CEntityIOOutput oput, CEntityInstance actvtr, CEntityInstance cllr, CVariant vlue)
        {
            var output = oput;
            var activator = actvtr;
            var caller = cllr;
            var value = vlue;

            try
            {
                if (activator == null || output == null || value == null || caller == null)
                {
                    SharpTimerDebug("Null reference detected in trigger_push hook.");
                    return HookResult.Continue;
                }

                if (activator.DesignerName != "player" || triggerPushFixEnabled == false)
                {
                    return HookResult.Continue;
                }

                var player = new CCSPlayerController(new CCSPlayerPawn(activator.Handle!).Controller.Value!.Handle);

                if (player == null)
                {
                    SharpTimerDebug("Player is null in trigger_push hook.");
                    return HookResult.Continue;
                }

                if (!IsAllowedPlayer(player)) return HookResult.Continue;

                if (triggerPushData.TryGetValue(caller.Handle, out TriggerPushData? TriggerPushData) && triggerPushFixEnabled == true && player.PlayerPawn.Value!.AbsVelocity.Length() > TriggerPushData.PushSpeed)
                {
                    player.PlayerPawn.Value!.AbsVelocity.X += TriggerPushData.PushDirEntitySpace.X * TriggerPushData.PushSpeed;
                    player.PlayerPawn.Value!.AbsVelocity.Y += TriggerPushData.PushDirEntitySpace.Y * TriggerPushData.PushSpeed;
                    player.PlayerPawn.Value!.AbsVelocity.Z += TriggerPushData.PushDirEntitySpace.Z * TriggerPushData.PushSpeed;
                    SharpTimerDebug($"trigger_push OnStartTouch Player velocity adjusted for {player.PlayerName} by {TriggerPushData.PushSpeed}");
                }

                return HookResult.Continue;
            }
            catch (Exception ex)
            {
                SharpTimerError($"Exception in trigger_push hook: {ex.Message}");
                return HookResult.Continue;
            }
        }
    }
}