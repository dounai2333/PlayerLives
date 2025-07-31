using EFT;
using EFT.HealthSystem;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT.InventoryLogic;
using EFT.Communications;
using PlayerLives.Features;
using PlayerLives.Helpers;

namespace PlayerLives.Patches
{
    internal class DeathPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ActiveHealthController), nameof(ActiveHealthController.Kill));
        }

        [PatchPrefix]
        static bool Prefix(ActiveHealthController __instance, EDamageType damageType)
        {
            try
            {
                // Get the Player field
                FieldInfo playerField = AccessTools.Field(typeof(ActiveHealthController), "Player");
                if (playerField == null) return true;

                Player player = playerField.GetValue(__instance) as Player;
                if (player == null) return true;

                if (!player.IsYourPlayer || player.IsAI) return true;

                string playerId = player.ProfileId;

                // Check if player is invulnerable from recent revival
                if (RevivalFeatures.IsPlayerInvulnerable(playerId))
                {
                    Plugin.LogSource.LogInfo($"Player {playerId} is invulnerable, blocking death completely");

                    return false; // Block the kill completely
                }

                // Check if player has remaining lives
                bool hasLives = Plugin.CurrentLives > 0;

                Plugin.LogSource.LogInfo($"DEATH PREVENTION: Player has lives: {hasLives || Settings.TESTING.Value}");

                if (hasLives || Settings.TESTING.Value)
                {
                    Plugin.LogSource.LogInfo("DEATH PREVENTION: Setting player to critical state instead of death");

                    // Set the player in critical state for the revival system
                    RevivalFeatures.SetPlayerCriticalState(player, true);

                    Plugin.CurrentLives--;

                    Plugin.LogSource.LogInfo($"DEATH PREVENTED: Player lives left {Plugin.CurrentLives}");

                    // Block the kill completely
                    return false;
                }
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogError($"Error in Death prevention patch: {ex.Message}");
            }

            return true;
        }
    }
}