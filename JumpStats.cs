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
                Console.WriteLine($"Player Jumped!");
            }
        }

        public void OnJumpStatTick(CCSPlayerController player, Vector velocity, Vector playerpos)
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
                }

                if (playerJumpStat.FramesOnGround == 1)
                {
                    if (playerJumpStat.Jumped)
                    {
                        double distance = Calculate2DDistanceWithVerticalMargins(ParseVector(playerJumpStat.JumpPos), playerpos);
                        if (distance != 0 && playerJumpStat.LastFramesOnGround > 2)
                        {
                            char color = GetJSColor(distance);
                            playerJumpStat.LastJumpType = "LJ";
                            player.PrintToChat(msgPrefix + $"{primaryChatColor}JumpStats: " + $"{ChatColors.Default}[{color}LJ{ChatColors.Default}]{ChatColors.Default}: " +
                                                            $"{color}{Math.Round((distance * 10) * 0.1, 3)}{ChatColors.Default} " +
                                                            $"[Pre:{Math.Round(ParseVector(playerJumpStat.LastSpeed).Length2D(), 3)} | Strafes: 0]");
                        }
                        else if (distance != 0 && playerJumpStat.LastFramesOnGround <= 2 && (playerJumpStat.LastJumpType == "LJ" || playerJumpStat.LastJumpType == "JB"))
                        {
                            char color = GetJSColor(distance);
                            playerJumpStat.LastJumpType = "BH";
                            player.PrintToChat(msgPrefix + $"{primaryChatColor}JumpStats: " + $"{ChatColors.Default}[{color}BH{ChatColors.Default}]: " +
                                                            $"{color}{Math.Round((distance * 10) * 0.1, 3)}{ChatColors.Default} " +
                                                            $"[Pre:{Math.Round(ParseVector(playerJumpStat.LastSpeed).Length2D(), 3)} | Strafes: 0]");
                        }
                        else if (distance != 0 && playerJumpStat.LastFramesOnGround <= 2 && (playerJumpStat.LastJumpType == "BH" || playerJumpStat.LastJumpType == "MBH" || playerJumpStat.LastJumpType == "JB"))
                        {
                            char color = GetJSColor(distance);
                            playerJumpStat.LastJumpType = "MBH";
                            player.PrintToChat(msgPrefix + $"{primaryChatColor}JumpStats: " + $"{ChatColors.Default}[{color}MBH{ChatColors.Default}]: " +
                                                            $"{color}{Math.Round((distance * 10) * 0.1, 3)}{ChatColors.Default} " +
                                                            $"[Pre:{Math.Round(ParseVector(playerJumpStat.LastSpeed).Length2D(), 3)} | Strafes: 0]");
                        }
                    }

                    playerJumpStat.Jumped = false;
                }
                else if (playerJumpStat.FramesOnGround == 0)
                {
                    if (playerJumpStat.Jumped)
                    {
                        double distance = Calculate2DDistanceWithVerticalMargins(ParseVector(playerJumpStat.OldJumpPos), playerpos);
                        if (distance != 0 && !playerJumpStat.LastOnGround && playerJumpStat.LastDucked && ((PlayerFlags)player.Pawn.Value.Flags & PlayerFlags.FL_DUCKING) != PlayerFlags.FL_DUCKING)
                        {
                            char color = GetJSColor(distance);
                            playerJumpStat.LastJumpType = "JB";
                            player.PrintToChat(msgPrefix + $"{primaryChatColor}JumpStats: " + $"{ChatColors.Default}[{color}JB{ChatColors.Default}]: " +
                                                            $"{color}{Math.Round((distance * 10) * 0.1, 3)}{ChatColors.Default} " +
                                                            $"[Pre:{Math.Round(ParseVector(playerJumpStat.LastSpeed).Length2D(), 3)} | Strafes: 0]");
                            playerJumpStat.LastFramesOnGround = playerJumpStat.FramesOnGround;
                        }
                    }
                }

                playerJumpStat.LastOnGround = playerJumpStat.OnGround;
                playerJumpStat.LastPos = $"{playerpos.X} {playerpos.Y} {playerpos.Z}";
                playerJumpStat.LastDucked = ((PlayerFlags)player.Pawn.Value.Flags & PlayerFlags.FL_DUCKING) == PlayerFlags.FL_DUCKING;
                if (playerJumpStat.OnGround) playerJumpStat.LastSpeed = $"{velocity.X} {velocity.Y} {velocity.Z}";
                if (playerJumpStat.OnGround) playerJumpStat.LastFramesOnGround = playerJumpStat.FramesOnGround;
            }
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

        static double Calculate2DDistanceWithVerticalMargins(Vector vector1, Vector vector2)
        {
            float verticalDistance = Math.Abs(vector1.Z - vector2.Z);

            if (verticalDistance > 31)
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
    }
}