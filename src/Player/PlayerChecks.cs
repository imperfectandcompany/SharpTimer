using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace SharpTimer
{
    public partial class SharpTimer
    {
        private bool IsAllowedPlayer(CCSPlayerController? player)
        {
            if (player == null || !player.IsValid || player.Pawn == null || !player.PlayerPawn.IsValid || !player.PawnIsAlive)
            {
                return false;
            }

            int playerSlot = player.Slot;

            CsTeam teamNum = (CsTeam)player.TeamNum;
            bool isTeamValid = teamNum == CsTeam.CounterTerrorist || teamNum == CsTeam.Terrorist;

            bool isTeamSpectatorOrNone = teamNum != CsTeam.Spectator && teamNum != CsTeam.None;
            bool isConnected = connectedPlayers.ContainsKey(playerSlot) && playerTimers.ContainsKey(playerSlot);
            bool isConnectedJS = !jumpStatsEnabled || playerJumpStats.ContainsKey(playerSlot);

            return isTeamValid && isTeamSpectatorOrNone && isConnected && isConnectedJS;
        }

        private bool IsAllowedSpectator(CCSPlayerController? player)
        {
            if (player == null || !player.IsValid || player.IsBot)
            {
                return false;
            }

            CsTeam teamNum = (CsTeam)player.TeamNum;
            bool isTeamValid = teamNum == CsTeam.Spectator;
            bool isConnected = connectedPlayers.ContainsKey(player.Slot) && playerTimers.ContainsKey(player.Slot);
            bool isObservingValid = player.Pawn?.Value.ObserverServices?.ObserverTarget != null &&
                                     specTargets.ContainsKey(player.Pawn.Value.ObserverServices.ObserverTarget.Index);

            return isTeamValid && isConnected && isObservingValid;
        }

        async Task IsPlayerATester(string steamId64, int playerSlot)
        {
            try
            {
                string response = await httpClient.GetStringAsync(testerPersonalGifsSource);

                using (JsonDocument jsonDocument = JsonDocument.Parse(response))
                {
                    if (playerTimers.TryGetValue(playerSlot, out PlayerTimerInfo? playerTimer))
                    {
                        playerTimer.IsTester = jsonDocument.RootElement.TryGetProperty(steamId64, out JsonElement steamData);

                        if (playerTimer.IsTester)
                        {
                            if (steamData.TryGetProperty("SmolGif", out JsonElement smolGifElement))
                            {
                                playerTimer.TesterSparkleGif = smolGifElement.GetString() ?? "";
                            }

                            if (steamData.TryGetProperty("BigGif", out JsonElement bigGifElement))
                            {
                                playerTimer.TesterPausedGif = bigGifElement.GetString() ?? "";
                            }
                        }
                    }
                    else
                    {
                        SharpTimerError($"Error in IsPlayerATester: player not on server anymore");
                    }
                }
            }
            catch (Exception ex)
            {
                SharpTimerError($"Error in IsPlayerATester: {ex.Message}");
            }
        }

        private void CheckPlayerCoords(CCSPlayerController? player, Vector playerSpeed)
        {
            try
            {
                if (player == null || !IsAllowedPlayer(player) || useTriggers == true)
                {
                    return;
                }

                Vector incorrectVector = new Vector(0, 0, 0);
                Vector? playerPos = player.Pawn?.Value.CBodyComponent?.SceneNode.AbsOrigin;

                if (playerPos == null || currentMapStartC1 == incorrectVector || currentMapStartC2 == incorrectVector ||
                    currentMapEndC1 == incorrectVector || currentMapEndC2 == incorrectVector)
                {
                    return;
                }

                bool isInsideStartBox = IsVectorInsideBox(playerPos, currentMapStartC1, currentMapStartC2);
                bool isInsideEndBox = IsVectorInsideBox(playerPos, currentMapEndC1, currentMapEndC2);

                if (!isInsideStartBox && isInsideEndBox)
                {
                    OnTimerStop(player);
                    if (enableReplays) OnRecordingStop(player);
                }
                else if (isInsideStartBox)
                {
                    OnTimerStart(player);
                    if (enableReplays) OnRecordingStart(player);

                    if ((maxStartingSpeedEnabled == true && use2DSpeed == false && Math.Round(playerSpeed.Length()) > maxStartingSpeed) ||
                        (maxStartingSpeedEnabled == true && use2DSpeed == true && Math.Round(playerSpeed.Length2D()) > maxStartingSpeed))
                    {
                        Action<CCSPlayerController?, float, bool> adjustVelocity = use2DSpeed ? AdjustPlayerVelocity2D : AdjustPlayerVelocity;
                        adjustVelocity(player, maxStartingSpeed, true);
                    }
                }
            }
            catch (Exception ex)
            {
                SharpTimerError($"Error in CheckPlayerCoords: {ex.Message}");
            }
        }

        private void CheckPlayerTriggerPushCoords(CCSPlayerController player)
        {
            try
            {
                if (player == null || !IsAllowedPlayer(player) || triggerPushData.Count == 0) return;

                Vector? playerPos = player.Pawn?.Value.CBodyComponent?.SceneNode.AbsOrigin;

                if (playerPos == null) return;

                var data = GetTriggerPushDataForVector(playerPos);
                if (data != null)
                {
                    (Vector pushDirEntitySpace, float pushSpeed) = data.Value;

                    pushDirEntitySpace = Normalize(pushDirEntitySpace);

                    Vector velocity = pushDirEntitySpace * pushSpeed;

                    player.PlayerPawn.Value.AbsVelocity.X = velocity.X;
                    player.PlayerPawn.Value.AbsVelocity.Y = velocity.Y;
                    player.PlayerPawn.Value.AbsVelocity.Z = velocity.Z;

                    SharpTimerDebug($"trigger_push fix: Player velocity set for {player.PlayerName} to {velocity.Length()}");
                }
            }
            catch (Exception ex)
            {
                SharpTimerError($"Error in CheckPlayerTriggerPushCoords: {ex.Message}");
            }
        }
    }
}