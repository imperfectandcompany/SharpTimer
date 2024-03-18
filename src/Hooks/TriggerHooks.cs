
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

                var player = new CCSPlayerController(new CCSPlayerPawn(activator.Handle).Controller.Value.Handle);

                if (player == null)
                {
                    SharpTimerDebug("Player is null in trigger_multiple OnStartTouch hook.");
                    return HookResult.Continue;
                }

                if (!IsAllowedPlayer(player) || caller.Entity.Name == null) return HookResult.Continue;

                /* if (caller.Entity.Name.ToString() == "bhop_block" && IsAllowedPlayer(player) && !playerTimers[player.Slot].IsTimerBlocked && playerTimers[player.Slot].TicksOnBhopBlock > bhopBlockTime)
                {
                    RespawnPlayer(player);
                    return HookResult.Continue;
                } */

                if (useStageTriggers == true && stageTriggers.ContainsKey(caller.Handle) && playerTimers[player.Slot].IsTimerBlocked == false && playerTimers[player.Slot].IsTimerRunning == true && IsAllowedPlayer(player))
                {
                    if (stageTriggers[caller.Handle] == 1)
                    {
                        playerTimers[player.Slot].CurrentMapStage = 1;
                        return HookResult.Continue;
                    }
                    else
                    {
                        _ = HandlePlayerStageTimes(player, caller.Handle);
                        return HookResult.Continue;
                    }
                }

                if (useCheckpointTriggers == true && cpTriggers.ContainsKey(caller.Handle) && playerTimers[player.Slot].IsTimerBlocked == false && playerTimers[player.Slot].IsTimerRunning == true && IsAllowedPlayer(player))
                {
                    _ = HandlePlayerCheckpointTimes(player, caller.Handle);
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
                    playerTimers[player.Slot].TimerTicks = 0;
                    playerTimers[player.Slot].BonusTimerTicks = 0;
                    playerTimers[player.Slot].IsTimerRunning = false;
                    playerTimers[player.Slot].IsBonusTimerRunning = false;
                    if (stageTriggerCount != 0 && useStageTriggers == true)
                    {
                        playerTimers[player.Slot].StageTimes.Clear();
                        playerTimers[player.Slot].StageVelos.Clear();
                        playerTimers[player.Slot].CurrentMapStage = stageTriggers.GetValueOrDefault(caller.Handle, 0);
                    }
                    else if (cpTriggerCount != 0 && useStageTriggers == false)
                    {
                        playerTimers[player.Slot].StageTimes.Clear();
                        playerTimers[player.Slot].StageVelos.Clear();
                        playerTimers[player.Slot].CurrentMapCheckpoint = 0;
                    }

                    if ((maxStartingSpeedEnabled == true && use2DSpeed == false && Math.Round(player.PlayerPawn.Value.AbsVelocity.Length()) > maxStartingSpeed) ||
                        (maxStartingSpeedEnabled == true && use2DSpeed == true && Math.Round(player.PlayerPawn.Value.AbsVelocity.Length2D()) > maxStartingSpeed))
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
                    playerTimers[player.Slot].TimerTicks = 0;
                    playerTimers[player.Slot].BonusTimerTicks = 0;
                    playerTimers[player.Slot].IsTimerRunning = false;
                    playerTimers[player.Slot].IsBonusTimerRunning = false;

                    if ((maxStartingSpeedEnabled == true && use2DSpeed == false && Math.Round(player.PlayerPawn.Value.AbsVelocity.Length()) > maxStartingSpeed) ||
                        (maxStartingSpeedEnabled == true && use2DSpeed == true && Math.Round(player.PlayerPawn.Value.AbsVelocity.Length2D()) > maxStartingSpeed))
                    {
                        Action<CCSPlayerController?, float, bool> adjustVelocity = use2DSpeed ? AdjustPlayerVelocity2D : AdjustPlayerVelocity;
                        adjustVelocity(player, maxStartingSpeed, false);
                    }
                    SharpTimerDebug($"Player {player.PlayerName} entered Bonus{startBonusX} StartZone");
                    return HookResult.Continue;
                }

                if (IsValidStopTriggerName(caller.Entity.Name.ToString()))
                {
                    playerTimers[player.Slot].TimerTicks = 0;
                    playerTimers[player.Slot].BonusTimerTicks = 0;
                    playerTimers[player.Slot].IsTimerRunning = false;
                    playerTimers[player.Slot].IsBonusTimerRunning = false;
                    player.PrintToChat(msgPrefix + $"Timer cancelled due to illegal skip attempt");
                }

                if (IsValidStopTriggerName(caller.Entity.Name.ToString()))
                {
                    playerTimers[player.Slot].TimerTicks = 0;
                    playerTimers[player.Slot].BonusTimerTicks = 0;
                    playerTimers[player.Slot].IsTimerRunning = false;
                    playerTimers[player.Slot].IsBonusTimerRunning = false;
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

        public HookResult TriggerMultipleOnEndTouch(CEntityIOOutput output, CEntityInstance activator, CEntityInstance caller, CVariant value)
        {
            try
            {
                if (activator == null || output == null || value == null || caller == null)
                {
                    SharpTimerDebug("Null reference detected in trigger_multiple OnEndTouch hook.");
                    return HookResult.Continue;
                }

                if (activator.DesignerName != "player" || useTriggers == false) return HookResult.Continue;

                var player = new CCSPlayerController(new CCSPlayerPawn(activator.Handle).Controller.Value.Handle);

                if (player == null)
                {
                    SharpTimerDebug("Player is null in trigger_multiple OnEndTouch hook.");
                    return HookResult.Continue;
                }

                if (!IsAllowedPlayer(player) || caller.Entity.Name == null) return HookResult.Continue;

                /* if (caller.Entity.Name.ToString() == "bhop_block" && IsAllowedPlayer(player) && !playerTimers[player.Slot].IsTimerBlocked)
                {
                    playerTimers[player.Slot].TicksOnBhopBlock = 0;

                    return HookResult.Continue;
                } */

                if (IsValidStartTriggerName(caller.Entity.Name.ToString()) && IsAllowedPlayer(player) && !playerTimers[player.Slot].IsTimerBlocked)
                {
                    OnTimerStart(player);
                    if (enableReplays) OnRecordingStart(player);

                    if (((maxStartingSpeedEnabled == true && use2DSpeed == false && Math.Round(player.PlayerPawn.Value.AbsVelocity.Length()) > maxStartingSpeed) ||
                        (maxStartingSpeedEnabled == true && use2DSpeed == true && Math.Round(player.PlayerPawn.Value.AbsVelocity.Length2D()) > maxStartingSpeed)) &&
                        !currentMapOverrideMaxSpeedLimit.Contains(caller.Entity.Name.ToString()) && currentMapOverrideMaxSpeedLimit != null)
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

                    if (((maxStartingSpeedEnabled == true && use2DSpeed == false && Math.Round(player.PlayerPawn.Value.AbsVelocity.Length()) > maxStartingSpeed) ||
                        (maxStartingSpeedEnabled == true && use2DSpeed == true && Math.Round(player.PlayerPawn.Value.AbsVelocity.Length2D()) > maxStartingSpeed)) &&
                        !currentMapOverrideMaxSpeedLimit.Contains(caller.Entity.Name.ToString()) && currentMapOverrideMaxSpeedLimit != null)
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

        public HookResult TriggerTeleportOnStartTouch(CEntityIOOutput output, CEntityInstance activator, CEntityInstance caller, CVariant value)
        {
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

                var player = new CCSPlayerController(new CCSPlayerPawn(activator.Handle).Controller.Value.Handle);

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

        public HookResult TriggerTeleportOnEndTouch(CEntityIOOutput output, CEntityInstance activator, CEntityInstance caller, CVariant value)
        {
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

                var player = new CCSPlayerController(new CCSPlayerPawn(activator.Handle).Controller.Value.Handle);

                if (player == null)
                {
                    SharpTimerDebug("Player is null in trigger_teleport hook.");
                    return HookResult.Continue;
                }

                if (!IsAllowedPlayer(player)) return HookResult.Continue;

                if (IsAllowedPlayer(player) && resetTriggerTeleportSpeedEnabled && currentMapOverrideDisableTelehop != null)
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
    }
}