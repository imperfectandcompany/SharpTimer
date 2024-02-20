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
                playerJumpStats[playerSlot].JumpedTick = Server.TickCount;
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
                            PrintJS(player, playerJumpStat.LastJumpType, distance, playerJumpStat.LastSpeed);
                        }
                        else if (distance != 0 && playerJumpStat.LastFramesOnGround <= 2 && (playerJumpStat.LastJumpType == "LJ" || playerJumpStat.LastJumpType == "JB"))
                        {
                            playerJumpStat.LastJumpType = "BH";
                            PrintJS(player, playerJumpStat.LastJumpType, distance, playerJumpStat.LastSpeed);
                        }
                        else if (distance != 0 && playerJumpStat.LastFramesOnGround <= 2 && (playerJumpStat.LastJumpType == "BH" || playerJumpStat.LastJumpType == "MBH" || playerJumpStat.LastJumpType == "JB"))
                        {
                            playerJumpStat.LastJumpType = "MBH";
                            PrintJS(player, playerJumpStat.LastJumpType, distance, playerJumpStat.LastSpeed);
                        }
                    }

                    playerJumpStat.Jumped = false;
                    playerJumpStat.LandedFromSound = false;
                }
                else if (playerJumpStat.LandedFromSound == true) //workaround for PlayerFlags.FL_ONGROUND being 1 frame late
                {
                    if (playerJumpStat.Jumped)
                    {
                        double distance = Calculate2DDistanceWithVerticalMargins(ParseVector(playerJumpStat.OldJumpPos), playerpos, true);
                        if (distance != 0 && !playerJumpStat.LastOnGround && playerJumpStat.LastDucked && ((PlayerFlags)player.Pawn.Value.Flags & PlayerFlags.FL_DUCKING) != PlayerFlags.FL_DUCKING)
                        {
                            playerJumpStat.LastJumpType = "JB";
                            PrintJS(player, playerJumpStat.LastJumpType, distance, playerJumpStat.LastSpeed);
                            playerJumpStat.LastFramesOnGround = playerJumpStat.FramesOnGround;
                            playerJumpStat.Jumped = true; // assume player jumped again if JB is successful
                        }
                    }
                    else
                    {
                        playerJumpStat.Jumped = false;
                    }
                    playerJumpStat.LandedFromSound = false;
                    playerJumpStat.FramesOnGround++;
                }

                playerJumpStat.LastOnGround = playerJumpStat.OnGround;
                playerJumpStat.LastPos = $"{playerpos.X} {playerpos.Y} {playerpos.Z}";
                playerJumpStat.LastDucked = ((PlayerFlags)player.Pawn.Value.Flags & PlayerFlags.FL_DUCKING) == PlayerFlags.FL_DUCKING;
                if (playerJumpStat.OnGround) playerJumpStat.LastSpeed = $"{velocity.X} {velocity.Y} {velocity.Z}";
                if (playerJumpStat.OnGround) playerJumpStat.LastFramesOnGround = playerJumpStat.FramesOnGround;
            }
        }

        public static void OnJumpStatTickInAir(CCSPlayerController player, PlayerJumpStats playerJumpStat, PlayerButtons? buttons, Vector playerpos, Vector velocity)
        {
            ulong button;
            if ((buttons & PlayerButtons.Moveleft) != 0 && (buttons & PlayerButtons.Moveright) != 0)
                button = 0;
            else if ((buttons & PlayerButtons.Moveleft) != 0)
                button = (ulong)PlayerButtons.Moveleft;
            else if ((buttons & PlayerButtons.Moveright) != 0)
                button = (ulong)PlayerButtons.Moveright;
            else
                button = 0;

            var LastJumpFrame = playerJumpStat.jumpFrames.Last();

            if (LastJumpFrame.LastButton != (ulong?)PlayerButtons.Moveleft && LastJumpFrame.LastButton != 0)
                playerJumpStat.Strafes++;
            else if (LastJumpFrame.LastButton != (ulong?)PlayerButtons.Moveright && LastJumpFrame.LastButton != 0)
                playerJumpStat.Strafes++;

            double height;
            if (IsVectorHigherThan(playerpos, ParseVector(LastJumpFrame.PositionString)))
                height = playerpos.Z - ParseVector(playerJumpStat.LastPos).Z;
            else
                height = LastJumpFrame?.LastHeight ?? 0;
            
            var JumpFrame = new PlayerJumpStats.JumpFrames
            {
                PositionString = $"{playerpos.X} {playerpos.Y} {playerpos.Z}",
                SpeedString = $"{velocity.X} {velocity.Y} {velocity.Z}",
                LastButton = button,
                LastHeight = height
            };

            playerJumpStat.jumpFrames.Add(JumpFrame);
        }

        public static char GetJSColor(double distance)
        {
            if (distance < 230)
            {
                return ChatColors.LightBlue;
            }
            else if (distance < 235)
            {
                return ChatColors.Blue;
            }
            else if (distance < 240)
            {
                return ChatColors.Purple;
            }
            else if (distance < 244)
            {
                return ChatColors.LightRed;
            }
            else if (distance < 246)
            {
                return ChatColors.Red;
            }
            else
            {
                return ChatColors.Gold;
            }
        }

        static double Calculate2DDistanceWithVerticalMargins(Vector vector1, Vector vector2, bool noVertCheck = false)
        {
            float verticalDistance = Math.Abs(vector1.Z - vector2.Z);

            if (verticalDistance > 31 && noVertCheck == false)
            {
                return 0;
            }

            double distance2D = Distance(vector1, vector2);

            if (distance2D > 50)
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

        public void PrintJS(CCSPlayerController player, string type, double distance, string lastSpeed)
        {
            char color = GetJSColor(distance);
            player.PrintToChat(msgPrefix + $"{primaryChatColor}JumpStats: {ChatColors.Grey}" +
                                            $"{type}: {color}{Math.Round((distance * 10) * 0.1, 3)}{ChatColors.Grey} | " +
                                            $"Pre: {primaryChatColor}{Math.Round(ParseVector(lastSpeed).Length2D(), 3)}{ChatColors.Grey} | " +
                                            $"Max {primaryChatColor}0{ChatColors.Grey} | " +
                                            $"Strafes: {primaryChatColor}0{ChatColors.Grey} | " +
                                            $"Height {primaryChatColor}0{ChatColors.Grey} | " +
                                            $"Width {primaryChatColor}0{ChatColors.Grey}");
        }
    }
}