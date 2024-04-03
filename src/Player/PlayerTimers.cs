using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace SharpTimer
{
    public partial class SharpTimer
    {
        public void OnTimerStart(CCSPlayerController? player, int bonusX = 0)
        {
            if (!IsAllowedPlayer(player)) return;

            if (bonusX != 0)
            {
                if (useTriggers) SharpTimerDebug($"Starting Bonus Timer for {player.PlayerName}");

                // Remove checkpoints for the current player
                playerCheckpoints.Remove(player.Slot);

                playerTimers[player.Slot].IsTimerRunning = false;
                playerTimers[player.Slot].TimerTicks = 0;

                playerTimers[player.Slot].IsBonusTimerRunning = true;
                playerTimers[player.Slot].BonusTimerTicks = 0;
                playerTimers[player.Slot].BonusStage = bonusX;
            }
            else
            {
                if (useTriggers) SharpTimerDebug($"Starting Timer for {player.PlayerName}");

                // Remove checkpoints for the current player
                playerCheckpoints.Remove(player.Slot);

                playerTimers[player.Slot].IsTimerRunning = true;
                playerTimers[player.Slot].TimerTicks = 0;

                playerTimers[player.Slot].IsBonusTimerRunning = false;
                playerTimers[player.Slot].BonusTimerTicks = 0;
                playerTimers[player.Slot].BonusStage = bonusX;
            }

            playerTimers[player.Slot].IsRecordingReplay = true;

        }

        public void OnTimerStop(CCSPlayerController? player)
        {
            if (!IsAllowedPlayer(player) || playerTimers[player.Slot].IsTimerRunning == false) return;

            if (useStageTriggers == true && useCheckpointTriggers == true)
            {
                if (playerTimers[player.Slot].CurrentMapStage != stageTriggerCount && currentMapOverrideStageRequirement == true)
                {
                    player.PrintToChat(msgPrefix + $"{ChatColors.LightRed} Error Saving Time: Player current stage does not match final one ({stageTriggerCount})");
                    playerTimers[player.Slot].IsTimerRunning = false;
                    playerTimers[player.Slot].IsRecordingReplay = false;
                    return;
                }

                if (playerTimers[player.Slot].CurrentMapCheckpoint != cpTriggerCount)
                {
                    player.PrintToChat(msgPrefix + $"{ChatColors.LightRed} Error Saving Time: Player current checkpoint does not match final one ({cpTriggerCount})");
                    playerTimers[player.Slot].IsTimerRunning = false;
                    playerTimers[player.Slot].IsRecordingReplay = false;
                    return;
                }
            }

            if (useStageTriggers == true && useCheckpointTriggers == false)
            {
                if (playerTimers[player.Slot].CurrentMapStage != stageTriggerCount && currentMapOverrideStageRequirement == true)
                {
                    player.PrintToChat(msgPrefix + $"{ChatColors.LightRed} Error Saving Time: Player current stage does not match final one ({stageTriggerCount})");
                    playerTimers[player.Slot].IsTimerRunning = false;
                    playerTimers[player.Slot].IsRecordingReplay = false;
                    return;
                }
            }

            if (useStageTriggers == false && useCheckpointTriggers == true)
            {
                if (playerTimers[player.Slot].CurrentMapCheckpoint != cpTriggerCount)
                {
                    player.PrintToChat(msgPrefix + $"{ChatColors.LightRed} Error Saving Time: Player current checkpoint does not match final one ({cpTriggerCount})");
                    playerTimers[player.Slot].IsTimerRunning = false;
                    playerTimers[player.Slot].IsRecordingReplay = false;
                    return;
                }
            }

            if (useTriggers) SharpTimerDebug($"Stopping Timer for {player.PlayerName}");

            int currentTicks = playerTimers[player.Slot].TimerTicks;

            SavePlayerTime(player, currentTicks);
            if (useMySQL == true) _ = SavePlayerTimeToDatabase(player, currentTicks, player.SteamID.ToString(), player.PlayerName, player.Slot);
            playerTimers[player.Slot].IsTimerRunning = false;
            playerTimers[player.Slot].IsRecordingReplay = false;

            if (useMySQL == false) _ = RankCommandHandler(player, player.SteamID.ToString(), player.Slot, player.PlayerName, true);
        }

        public void OnBonusTimerStop(CCSPlayerController? player, int bonusX)
        {
            if (!IsAllowedPlayer(player) || playerTimers[player.Slot].IsBonusTimerRunning == false) return;

            if (useTriggers) SharpTimerDebug($"Stopping Bonus Timer for {player.PlayerName}");

            int currentTicks = playerTimers[player.Slot].BonusTimerTicks;

            SavePlayerTime(player, currentTicks, bonusX);
            if (useMySQL == true) _ = SavePlayerTimeToDatabase(player, currentTicks, player.SteamID.ToString(), player.PlayerName, player.Slot, bonusX);
            playerTimers[player.Slot].IsBonusTimerRunning = false;
            playerTimers[player.Slot].IsRecordingReplay = false;
        }

        public void SavePlayerTime(CCSPlayerController? player, int timerTicks, int bonusX = 0)
        {
            if (!IsAllowedPlayer(player)) return;
            if ((bonusX == 0 && playerTimers[player.Slot].IsTimerRunning == false) || (bonusX != 0 && playerTimers[player.Slot].IsBonusTimerRunning == false)) return;

            SharpTimerDebug($"Saving player {(bonusX != 0 ? $"bonus {bonusX} time" : "time")} of {timerTicks} ticks for {player.PlayerName} to json");
            string mapRecordsPath = Path.Combine(playerRecordsPath, bonusX == 0 ? $"{currentMapName}.json" : $"{currentMapName}_bonus{bonusX}.json");

            try
            {
                using (JsonDocument jsonDocument = LoadJson(mapRecordsPath))
                {
                    Dictionary<string, PlayerRecord> records;

                    if (jsonDocument != null)
                    {
                        string json = jsonDocument.RootElement.GetRawText();
                        records = JsonSerializer.Deserialize<Dictionary<string, PlayerRecord>>(json) ?? new Dictionary<string, PlayerRecord>();
                    }
                    else
                    {
                        records = new Dictionary<string, PlayerRecord>();
                    }

                    string steamId = player.SteamID.ToString();
                    string playerName = player.PlayerName;

                    if (!records.ContainsKey(steamId) || records[steamId].TimerTicks > timerTicks)
                    {
                        if (!useMySQL) _ = PrintMapTimeToChat(player, player.PlayerName, records.GetValueOrDefault(steamId)?.TimerTicks ?? 0, timerTicks, bonusX);

                        records[steamId] = new PlayerRecord
                        {
                            PlayerName = playerName,
                            TimerTicks = timerTicks
                        };

                        string updatedJson = JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(mapRecordsPath, updatedJson);

                        if ((stageTriggerCount != 0 || cpTriggerCount != 0) && bonusX == 0 && useMySQL == false) DumpPlayerStageTimesToJson(player);
                        if (enableReplays == true && useMySQL == false) DumpReplayToJson(player, bonusX);
                    }
                    else
                    {
                        if (!useMySQL) _ = PrintMapTimeToChat(player, player.PlayerName, records[steamId].TimerTicks, timerTicks, bonusX);
                    }
                }
            }
            catch (Exception ex)
            {
                SharpTimerError($"Error in SavePlayerTime: {ex.Message}");
            }
        }

        private async Task HandlePlayerStageTimes(CCSPlayerController player, nint triggerHandle)
        {
            try
            {
                if (!IsAllowedPlayer(player))
                {
                    return;
                }

                SharpTimerDebug($"Player {player.PlayerName} has a stage trigger with handle {triggerHandle}");

                if (stageTriggers.TryGetValue(triggerHandle, out int stageTrigger))
                {
                    var playerSlot = player.Slot;
                    var playerSteamID = player.SteamID.ToString();
                    var playerName = player.PlayerName;
                    var playerTimerTicks = playerTimers[playerSlot].TimerTicks; // store so its in sync with player

                    var (srSteamID, srPlayerName, srTime) = ("null", "null", "null");
                    if (playerTimers[playerSlot].CurrentMapStage == stageTrigger || playerTimers[playerSlot] == null) return;
                    if (useMySQL == true)
                    {
                        (srSteamID, srPlayerName, srTime) = await GetMapRecordSteamIDFromDatabase();
                    }
                    else
                    {
                        (srSteamID, srPlayerName, srTime) = GetMapRecordSteamID();
                    }

                    Server.NextFrame(() =>
                    {
                        if (!IsAllowedPlayer(player)) return;
                        if (playerTimers.TryGetValue(playerSlot, out PlayerTimerInfo? playerTimer))
                        {

                            if (playerTimer.CurrentMapStage == stageTrigger || playerTimer == null) return;

                            var (previousStageTime, previousStageSpeed) = GetStageTime(playerSteamID, stageTrigger);
                            var (srStageTime, srStageSpeed) = GetStageTime(srSteamID, stageTrigger);

                            string currentStageSpeed = Math.Round(use2DSpeed ? Math.Sqrt(player.PlayerPawn.Value.AbsVelocity.X * player.PlayerPawn.Value.AbsVelocity.X + player.PlayerPawn.Value.AbsVelocity.Y * player.PlayerPawn.Value.AbsVelocity.Y)
                                                                                : Math.Sqrt(player.PlayerPawn.Value.AbsVelocity.X * player.PlayerPawn.Value.AbsVelocity.X + player.PlayerPawn.Value.AbsVelocity.Y * player.PlayerPawn.Value.AbsVelocity.Y + player.PlayerPawn.Value.AbsVelocity.Z * player.PlayerPawn.Value.AbsVelocity.Z))
                                                                                .ToString("0000");

                            if (previousStageTime != 0)
                            {
                                player.PrintToChat(msgPrefix + $" Entering Stage: {stageTrigger}");
                                player.PrintToChat(msgPrefix + $" Time: {ChatColors.White}[{primaryChatColor}{FormatTime(playerTimerTicks)}{ChatColors.White}] " +
                                                               $" [{FormatTimeDifference(playerTimerTicks, previousStageTime)}{ChatColors.White}]" +
                                                               $" {(previousStageTime != srStageTime ? $"[SR {FormatTimeDifference(playerTimerTicks, srStageTime)}{ChatColors.White}]" : "")}");

                                if (float.TryParse(currentStageSpeed, out float speed) && speed >= 100) //workaround for staged maps with not telehops
                                    player.PrintToChat(msgPrefix + $" Speed: {ChatColors.White}[{primaryChatColor}{currentStageSpeed}u/s{ChatColors.White}]" +
                                                                    $" [{FormatSpeedDifferenceFromString(currentStageSpeed, previousStageSpeed)}u/s{ChatColors.White}]" +
                                                                    $" {(previousStageSpeed != srStageSpeed ? $"[SR {FormatSpeedDifferenceFromString(currentStageSpeed, srStageSpeed)}u/s{ChatColors.White}]" : "")}");
                            }

                            if (playerTimer.StageVelos != null && playerTimer.StageTimes != null && playerTimer.IsTimerRunning == true && IsAllowedPlayer(player))
                            {
                                try
                                {
                                    playerTimer.StageTimes[stageTrigger] = playerTimerTicks;
                                    playerTimer.StageVelos[stageTrigger] = $"{currentStageSpeed}";
                                    SharpTimerDebug($"Player {playerName} Entering stage {stageTrigger} Time {playerTimer.StageTimes[stageTrigger]}");
                                }
                                catch (Exception ex)
                                {
                                    SharpTimerError($"Error updating StageTimes dictionary: {ex.Message}");
                                    SharpTimerDebug($"Player {playerName} dictionary keys: {string.Join(", ", playerTimer.StageTimes.Keys)}");
                                }
                            }

                            playerTimer.CurrentMapStage = stageTrigger;
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                SharpTimerError($"Error in HandlePlayerStageTimes: {ex.Message}");
            }
        }

        private async Task HandlePlayerCheckpointTimes(CCSPlayerController player, nint triggerHandle)
        {
            try
            {
                if (!IsAllowedPlayer(player))
                {
                    return;
                }

                if (cpTriggers.TryGetValue(triggerHandle, out int cpTrigger))
                {

                    var playerSlot = player.Slot;
                    var playerSteamID = player.SteamID.ToString();
                    var playerName = player.PlayerName;
                    if (useStageTriggers == true) //use stagetime instead
                    {
                        playerTimers[playerSlot].CurrentMapCheckpoint = cpTrigger;
                        return;
                    }

                    SharpTimerDebug($"Player {playerName} has a checkpoint trigger with handle {triggerHandle}");

                    var playerTimerTicks = playerTimers[playerSlot].TimerTicks; // store so its in sync with player

                    var (srSteamID, srPlayerName, srTime) = ("null", "null", "null");
                    if (playerTimers[playerSlot].CurrentMapCheckpoint == cpTrigger || playerTimers[playerSlot] == null) return;
                    if (useMySQL == true)
                    {
                        (srSteamID, srPlayerName, srTime) = await GetMapRecordSteamIDFromDatabase();
                    }
                    else
                    {
                        (srSteamID, srPlayerName, srTime) = GetMapRecordSteamID();
                    }

                    Server.NextFrame(() =>
                    {
                        if (!IsAllowedPlayer(player)) return;
                        if (playerTimers.TryGetValue(playerSlot, out PlayerTimerInfo? playerTimer))
                        {

                            if (playerTimer.CurrentMapCheckpoint == cpTrigger || playerTimer == null) return;

                            var (previousStageTime, previousStageSpeed) = GetStageTime(playerSteamID, cpTrigger);
                            var (srStageTime, srStageSpeed) = GetStageTime(srSteamID, cpTrigger);

                            string currentStageSpeed = Math.Round(use2DSpeed ? Math.Sqrt(player.PlayerPawn.Value.AbsVelocity.X * player.PlayerPawn.Value.AbsVelocity.X + player.PlayerPawn.Value.AbsVelocity.Y * player.PlayerPawn.Value.AbsVelocity.Y)
                                                                                : Math.Sqrt(player.PlayerPawn.Value.AbsVelocity.X * player.PlayerPawn.Value.AbsVelocity.X + player.PlayerPawn.Value.AbsVelocity.Y * player.PlayerPawn.Value.AbsVelocity.Y + player.PlayerPawn.Value.AbsVelocity.Z * player.PlayerPawn.Value.AbsVelocity.Z))
                                                                                .ToString("0000");

                            if (previousStageTime != 0)
                            {
                                player.PrintToChat(msgPrefix + $" Checkpoint: {cpTrigger}");
                                player.PrintToChat(msgPrefix + $" Time: {ChatColors.White}[{primaryChatColor}{FormatTime(playerTimerTicks)}{ChatColors.White}] " +
                                                               $" [{FormatTimeDifference(playerTimerTicks, previousStageTime)}{ChatColors.White}]" +
                                                               $" {(previousStageTime != srStageTime ? $"[SR {FormatTimeDifference(playerTimerTicks, srStageTime)}{ChatColors.White}]" : "")}");

                                if (float.TryParse(currentStageSpeed, out float speed) && speed >= 100) //workaround for staged maps with not telehops
                                    player.PrintToChat(msgPrefix + $" Speed: {ChatColors.White}[{primaryChatColor}{currentStageSpeed}u/s{ChatColors.White}]" +
                                                                   $" [{FormatSpeedDifferenceFromString(currentStageSpeed, previousStageSpeed)}u/s{ChatColors.White}]" +
                                                                   $" {(previousStageSpeed != srStageSpeed ? $"[SR {FormatSpeedDifferenceFromString(currentStageSpeed, srStageSpeed)}u/s{ChatColors.White}]" : "")}");
                            }

                            if (playerTimer.StageVelos != null && playerTimer.StageTimes != null && playerTimer.IsTimerRunning == true && IsAllowedPlayer(player))
                            {
                                if (!playerTimer.StageTimes.ContainsKey(cpTrigger))
                                {
                                    SharpTimerDebug($"Player {playerName} cleared StageTimes before (cpTrigger)");
                                    playerTimer.StageTimes.Add(cpTrigger, playerTimerTicks);
                                    playerTimer.StageVelos.Add(cpTrigger, $"{currentStageSpeed}");
                                }
                                else
                                {
                                    try
                                    {
                                        playerTimer.StageTimes[cpTrigger] = playerTimerTicks;
                                        playerTimer.StageVelos[cpTrigger] = $"{currentStageSpeed}";
                                        SharpTimerDebug($"Player {playerName} Entering checkpoint {cpTrigger} Time {playerTimer.StageTimes[cpTrigger]}");
                                    }
                                    catch (Exception ex)
                                    {
                                        SharpTimerError($"Error updating StageTimes dictionary: {ex.Message}");
                                        SharpTimerDebug($"Player {playerName} dictionary keys: {string.Join(", ", playerTimer.StageTimes.Keys)}");
                                    }
                                }
                            }
                            playerTimer.CurrentMapCheckpoint = cpTrigger;
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                SharpTimerError($"Error in HandlePlayerCheckpointTimes: {ex.Message}");
            }
        }

        public void DumpPlayerStageTimesToJson(CCSPlayerController? player)
        {
            if (!IsAllowedPlayer(player)) return;

            string fileName = $"{currentMapName.ToLower()}_stage_times.json";
            string playerStageRecordsPath = Path.Join(gameDir, "csgo", "cfg", "SharpTimer", "PlayerStageData", fileName);

            try
            {
                using (JsonDocument jsonDocument = LoadJson(playerStageRecordsPath))
                {
                    if (jsonDocument != null)
                    {
                        string jsonContent = jsonDocument.RootElement.GetRawText();

                        Dictionary<string, PlayerStageData> playerData;
                        if (!string.IsNullOrEmpty(jsonContent))
                        {
                            playerData = JsonSerializer.Deserialize<Dictionary<string, PlayerStageData>>(jsonContent);
                        }
                        else
                        {
                            playerData = new Dictionary<string, PlayerStageData>();
                        }

                        string playerId = player.SteamID.ToString();

                        if (!playerData.ContainsKey(playerId))
                        {
                            playerData[playerId] = new PlayerStageData();
                        }

                        if (playerTimers.TryGetValue(player.Slot, out PlayerTimerInfo? playerTimer))
                        {
                            playerData[playerId].StageTimes = playerTimer.StageTimes;
                            playerData[playerId].StageVelos = playerTimer.StageVelos;
                        }
                        else
                        {
                            SharpTimerError($"Error in DumpPlayerStageTimesToJson: playerTimers does not have the requested playerSlot");
                        }

                        string updatedJson = JsonSerializer.Serialize(playerData, new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(playerStageRecordsPath, updatedJson);
                    }
                    else
                    {
                        Dictionary<string, PlayerStageData> playerData = new Dictionary<string, PlayerStageData>();

                        string playerId = player.SteamID.ToString();

                        if (playerTimers.TryGetValue(player.Slot, out PlayerTimerInfo? playerTimer))
                        {
                            playerData[playerId] = new PlayerStageData
                            {
                                StageTimes = playerTimers[player.Slot].StageTimes,
                                StageVelos = playerTimers[player.Slot].StageVelos
                            };
                        }
                        else
                        {
                            SharpTimerError($"Error in DumpPlayerStageTimesToJson: playerTimers does not have the requested playerSlot");
                        }

                        string updatedJson = JsonSerializer.Serialize(playerData, new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(playerStageRecordsPath, updatedJson);
                    }
                }
            }
            catch (Exception ex)
            {
                SharpTimerError($"Error in DumpPlayerStageTimesToJson: {ex.Message}");
            }
        }
    }
}