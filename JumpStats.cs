using System.Numerics;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace SharpTimer
{
    public partial class SharpTimer
    {
        //based on https://github.com/DEAFPS/cs2-kz-lua/blob/main/kz.lua
        public void OnJumpStatJumped(CCSPlayerController player)
        {
            if (IsAllowedPlayer(player))
            {
                int playerSlot = player.Slot;
                playerJumpStats[playerSlot].Jumped = true;
                playerJumpStats[playerSlot].OldJumpPos = string.IsNullOrEmpty(playerJumpStats[playerSlot].JumpPos)
                                                            ? $"{player.Pawn.Value.AbsOrigin.X} {player.Pawn.Value.AbsOrigin.Y} {player.Pawn.Value.AbsOrigin.Z}"
                                                            : playerJumpStats[playerSlot].JumpPos;
                playerJumpStats[playerSlot].JumpPos = $"{player.Pawn.Value.AbsOrigin.X} {player.Pawn.Value.AbsOrigin.Y} {player.Pawn.Value.AbsOrigin.Z}";
            }
        }

        public void OnJumpStatSound(CCSPlayerController player)
        {
            if (IsAllowedPlayer(player))
            {
                int playerSlot = player.Slot;
                if (playerJumpStats[playerSlot].Jumped == true && playerJumpStats[playerSlot].FramesOnGround == 0)
                {
                    playerJumpStats[playerSlot].LandedFromSound = true;
                }
            }
        }

        public void OnJumpStatTick(CCSPlayerController player, Vector velocity, Vector playerpos, PlayerButtons? buttons)
        {
            try
            {
                if (playerJumpStats.TryGetValue(player.Slot, out PlayerJumpStats? playerJumpStat))
                {
                    playerJumpStat.OnGround = ((PlayerFlags)player.Pawn.Value.Flags & PlayerFlags.FL_ONGROUND) == PlayerFlags.FL_ONGROUND; //need hull trace for this to detect surf etc

                    if (playerJumpStat.OnGround)
                    {
                        playerJumpStat.FramesOnGround++;
                    }
                    else
                    {
                        playerJumpStat.FramesOnGround = 0;
                        OnJumpStatTickInAir(player, playerJumpStat, buttons, playerpos, velocity);
                    }

                    if (playerJumpStat.FramesOnGround == 1)
                    {
                        if (playerJumpStat.Jumped)
                        {
                            double distance = Calculate2DDistanceWithVerticalMargins(ParseVector(playerJumpStat.JumpPos), playerpos);
                            if (distance != 0 && playerJumpStat.LastFramesOnGround > 2)
                            {
                                playerJumpStat.LastJumpType = "LJ";
                                PrintJS(player, playerJumpStat, distance);
                            }
                            else if (distance != 0 && playerJumpStat.LastFramesOnGround <= 2 && (playerJumpStat.LastJumpType == "LJ" || playerJumpStat.LastJumpType == "JB"))
                            {
                                playerJumpStat.LastJumpType = "BH";
                                PrintJS(player, playerJumpStat, distance);
                            }
                            else if (distance != 0 && playerJumpStat.LastFramesOnGround <= 2 && (playerJumpStat.LastJumpType == "BH" || playerJumpStat.LastJumpType == "MBH" || playerJumpStat.LastJumpType == "JB"))
                            {
                                playerJumpStat.LastJumpType = "MBH";
                                PrintJS(player, playerJumpStat, distance);
                            }
                        }

                        playerJumpStat.Jumped = false;
                        playerJumpStat.LandedFromSound = false;
                        playerJumpStat.jumpFrames.Clear();
                        playerJumpStat.Strafes = 0;
                        playerJumpStat.WTicks = 0;
                    }
                    else if (playerJumpStat.LandedFromSound == true) //workaround for PlayerFlags.FL_ONGROUND being 1 frame late
                    {
                        if (playerJumpStat.Jumped)
                        {
                            double distance = Calculate2DDistanceWithVerticalMargins(ParseVector(playerJumpStat.OldJumpPos), playerpos, true);
                            if (distance != 0 && !playerJumpStat.LastOnGround && playerJumpStat.LastDucked && ((PlayerFlags)player.Pawn.Value.Flags & PlayerFlags.FL_DUCKING) != PlayerFlags.FL_DUCKING)
                            {
                                playerJumpStat.LastJumpType = "JB";
                                PrintJS(player, playerJumpStat, distance);
                                playerJumpStat.LastFramesOnGround = playerJumpStat.FramesOnGround;
                                playerJumpStat.Jumped = true; // assume player jumped again if JB is successful
                            }
                        }
                        else
                        {
                            playerJumpStat.Jumped = false;
                        }
                        playerJumpStat.jumpFrames.Clear();
                        playerJumpStat.Strafes = 0;
                        playerJumpStat.WTicks = 0;
                        playerJumpStat.LandedFromSound = false;
                        playerJumpStat.FramesOnGround++;
                    }

                    playerJumpStat.LastOnGround = playerJumpStat.OnGround;
                    playerJumpStat.LastPos = $"{playerpos.X} {playerpos.Y} {playerpos.Z}";
                    playerJumpStat.LastDucked = ((PlayerFlags)player.Pawn.Value.Flags & PlayerFlags.FL_DUCKING) == PlayerFlags.FL_DUCKING;
                    if (playerJumpStat.OnGround)
                    {
                        playerJumpStat.LastSpeed = $"{velocity.X} {velocity.Y} {velocity.Z}";
                        playerJumpStat.LastFramesOnGround = playerJumpStat.FramesOnGround;
                        playerJumpStat.LastEyeAngle = $"{player.PlayerPawn.Value.EyeAngles.X} {player.PlayerPawn.Value.EyeAngles.Y} {player.PlayerPawn.Value.EyeAngles.Z}";
                    }
                }
            }
            catch (Exception ex)
            {
                SharpTimerDebug($"Exception in OnJumpStatTick: {ex}");
            }
        }

        public void OnJumpStatTickInAir(CCSPlayerController player, PlayerJumpStats playerJumpStat, PlayerButtons? buttons, Vector playerpos, Vector velocity)
        {
            try
            {
                var LastJumpFrame = playerJumpStat.jumpFrames.Any() ? playerJumpStat.jumpFrames.Last() : new PlayerJumpStats.JumpFrames
                {
                    PositionString = $" ",
                    SpeedString = $" ",
                    LastButton = 0,
                    MaxHeight = 0,
                    MaxSpeed = 0
                };

                ulong button;
                if ((buttons & PlayerButtons.Moveleft) != 0 && (buttons & PlayerButtons.Moveright) != 0)
                    button = 0;
                else if ((buttons & PlayerButtons.Moveleft) != 0)
                    button = (ulong)PlayerButtons.Moveleft;
                else if ((buttons & PlayerButtons.Moveright) != 0)
                    button = (ulong)PlayerButtons.Moveright;
                else
                    button = 0;

                if ((buttons & PlayerButtons.Forward) != 0)
                    playerJumpStat.WTicks++;

                if (LastJumpFrame.LastButton != button && LastJumpFrame.LastButton != 0)
                    playerJumpStat.Strafes++;

                double maxHeight;
                if (IsVectorHigherThan(playerpos, ParseVector(LastJumpFrame.PositionString)))
                    maxHeight = playerpos.Z - ParseVector(playerJumpStat.OldJumpPos).Z;
                else
                    maxHeight = LastJumpFrame?.MaxHeight ?? 0;

                double maxSpeed;
                if (velocity.Length2D() > LastJumpFrame.MaxSpeed)
                    maxSpeed = velocity.Length2D();
                else
                    maxSpeed = LastJumpFrame?.MaxSpeed ?? 0;

                var JumpFrame = new PlayerJumpStats.JumpFrames
                {
                    PositionString = $"{playerpos.X} {playerpos.Y} {playerpos.Z}",
                    SpeedString = $"{velocity.X} {velocity.Y} {velocity.Z}",
                    LastButton = button,
                    MaxHeight = maxHeight,
                    MaxSpeed = maxSpeed
                };

                playerJumpStat.jumpFrames.Add(JumpFrame);
            }
            catch (Exception ex)
            {
                SharpTimerDebug($"Exception in OnJumpStatTickInAir: {ex}");
            }
        }

        public static char GetJSColor(double distance)
        {
            if (distance < 230)
            {
                return ChatColors.Grey;
            }
            else if (distance < 235)
            {
                return ChatColors.Blue;
            }
            else if (distance < 240)
            {
                return ChatColors.Green;
            }
            else if (distance < 244)
            {
                return ChatColors.DarkRed;
            }
            else if (distance < 246)
            {
                return ChatColors.Gold;
            }
            else
            {
                return ChatColors.Purple;
            }
        }

        double Calculate2DDistanceWithVerticalMargins(Vector vector1, Vector vector2, bool noVertCheck = false)
        {
            if (vector1 == null || vector2 == null)
            {
                return 0;
            }

            float verticalDistance = Math.Abs(vector1.Z - vector2.Z);

            if (verticalDistance >= 32 && noVertCheck == false)
            {
                return 0;
            }

            double distance2D = Distance(vector1, vector2);

            if (distance2D > jumpStatsMinDist || noVertCheck == true)
            {
                double result = distance2D + 32.0f;
                return result;
            }
            else
            {
                return 0;
            }
        }

        public void InvalidateJS(int playerSlot)
        {
            if (playerJumpStats.TryGetValue(playerSlot, out PlayerJumpStats? value))
            {
                value.LastFramesOnGround = 0;
                value.Jumped = false;
            }
        }

        public void PrintJS(CCSPlayerController player, PlayerJumpStats playerJumpStat, double distance)
        {
            char color = GetJSColor(distance);
            player.PrintToChat(msgPrefix + $"{primaryChatColor}JumpStats: {ChatColors.Grey}" +
                                            $"{playerJumpStat.LastJumpType}: {color}{Math.Round((distance * 10) * 0.1, 3)}{ChatColors.Grey} | " +
                                            $"Pre: {primaryChatColor}{Math.Round(ParseVector(playerJumpStat.LastSpeed).Length2D(), 3)}{ChatColors.Grey} | " +
                                            $"Max: {primaryChatColor}{Math.Round(playerJumpStat.jumpFrames.Last().MaxSpeed, 3)}{ChatColors.Grey} | ");
            player.PrintToChat(msgPrefix + $"{ChatColors.Grey}Strafes: {primaryChatColor}{playerJumpStat.Strafes}{ChatColors.Grey} | " +
                                            $"Height: {primaryChatColor}{Math.Round(playerJumpStat.jumpFrames.Last().MaxHeight, 3)}{ChatColors.Grey} | " +
                                            $"WT: {primaryChatColor}{playerJumpStat.WTicks}{ChatColors.Grey}");
        }
    }
}