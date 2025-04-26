using EFT;
using EFT.HealthSystem;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT.InventoryLogic;
using UnityEngine;
using EFT.Communications;
using Comfort.Common;
using RevivalLite.Helpers;
using BepInEx;

namespace RevivalLite.Features
{
    /// <summary>
    /// Enhanced revival feature with manual activation and temporary invulnerability with restrictions
    /// </summary>
    internal class RevivalFeatures : ModulePatch
    {
        // New constants for effects
        private static readonly float MOVEMENT_SPEED_MULTIPLIER = 0.1f; // 40% normal speed during invulnerability
        private static readonly bool FORCE_CROUCH_DURING_INVULNERABILITY = false; // Force player to crouch during invulnerability
        private static readonly bool DISABLE_SHOOTING_DURING_INVULNERABILITY = false; // Disable shooting during invulnerability

        // States
        private static Dictionary<string, long> _lastRevivalTimesByPlayer = new Dictionary<string, long>();
        private static Dictionary<string, bool> _playerInCriticalState = new Dictionary<string, bool>();
        private static Dictionary<string, bool> _playerIsInvulnerable = new Dictionary<string, bool>();
        private static Dictionary<string, float> _playerInvulnerabilityTimers = new Dictionary<string, float>();
        private static Dictionary<string, EFT.PlayerAnimator.EWeaponAnimationType> _originalWeaponAnimationType = new Dictionary<string, PlayerAnimator.EWeaponAnimationType>();
        private static Player PlayerClient { get; set; } = null;

        protected override MethodBase GetTargetMethod()
        {
            // We're patching the Update method of Player to constantly check for revival key press
            return AccessTools.Method(typeof(Player), nameof(Player.UpdateTick));
        }

        [PatchPostfix]
        static void Postfix(Player __instance)
        {
            try
            {
                string playerId = __instance.ProfileId;
                PlayerClient = __instance;

                // Only proceed for the local player
                if (!__instance.IsYourPlayer)
                    return;

                // Update invulnerability timer if active
                if (_playerIsInvulnerable.TryGetValue(playerId, out bool isInvulnerable) && isInvulnerable)
                {
                    if (_playerInvulnerabilityTimers.TryGetValue(playerId, out float timer))
                    {
                        timer -= Time.deltaTime;
                        _playerInvulnerabilityTimers[playerId] = timer;

                        // Force player to crouch during invulnerability
                        if (FORCE_CROUCH_DURING_INVULNERABILITY)
                        {
                            // Force crouch state
                            if (__instance.MovementContext.PoseLevel > 0)
                            {
                                __instance.MovementContext.SetPoseLevel(0);
                            }
                        }

                        // Disable shooting during invulnerability
                        if (DISABLE_SHOOTING_DURING_INVULNERABILITY)
                        {
                            // Block shooting by canceling fire operations
                            if (__instance.HandsController.IsAiming)
                            {
                                __instance.HandsController.IsAiming = false;
                            }
                        }

                        // End invulnerability if timer is up
                        if (timer <= 0)
                        {
                            EndInvulnerability(__instance);
                        }
                    }
                }

                // Check for manual revival key press when in critical state
                if (_playerInCriticalState.TryGetValue(playerId, out bool inCritical) && inCritical && Constants.Constants.SELF_REVIVAL)
                {
                    if (Input.GetKeyDown(Settings.REVIVAL_KEY.Value))
                    {
                        TryPerformManualRevival(__instance);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogError($"Error in RevivalFeatureExtension patch: {ex.Message}");
            }
        }

        public static bool IsPlayerInCriticalState(string playerId)
        {
            return _playerInCriticalState.TryGetValue(playerId, out bool inCritical) && inCritical;
        }

        public static void SetPlayerCriticalState(Player player, bool criticalState)
        {
            if (player == null)
                return;

            string playerId = player.ProfileId;

            // Update critical state
            _playerInCriticalState[playerId] = criticalState;

            if (criticalState)
            {
                // Apply effects when entering critical state
                // Make player invulnerable while in critical state
                _playerIsInvulnerable[playerId] = true;

                // Apply tremor effect without healing
                ApplyCriticalEffects(player);

                // Make player invisible to AI - fixed implementation
                ApplyRevivableStatePlayer(player);

                if (player.IsYourPlayer)
                {
                    try
                    {
                        // Show revival message
                        NotificationManagerClass.DisplayMessageNotification(
                            $"CRITICAL CONDITION! Press {Settings.REVIVAL_KEY.Value.ToString()} to use your defibrillator!",
                            ENotificationDurationType.Long,
                            ENotificationIconType.Default,
                            Color.red);
                    }
                    catch (Exception ex)
                    {
                        Plugin.LogSource.LogError($"Error displaying critical state UI: {ex.Message}");
                    }
                }
            }
            else
            {

                // If player is leaving critical state without revival (e.g., revival failed),
                // make sure to remove stealth from player and disable invulnerability
                if (!_playerInvulnerabilityTimers.ContainsKey(playerId))
                {
                    _playerIsInvulnerable.Remove(playerId);
                }
            }
        }

        // Apply effects for critical state without healing
        private static void ApplyCriticalEffects(Player player)
        {
            try
            {
                string playerId = player.ProfileId;

                // Don't apply random effects on wake up? Can't see.
                // player.ActiveHealthController.DoContusion(Settings.REVIVAL_DURATION.Value, 1f);
                // player.ActiveHealthController.DoStun(Settings.REVIVAL_DURATION.Value / 2, 1f);

                // Restrict player to crouch-only
                if (player.MovementContext != null)
                {
                    // Force crouch
                    player.MovementContext.SetPoseLevel(0);

                    // Disable sprinting
                    player.ActiveHealthController.AddFatigue();
                    player.ActiveHealthController.SetStaminaCoeff(0f);
                }

                Plugin.LogSource.LogDebug($"Applied critical effects to player {playerId}");
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogError($"Error applying critical effects: {ex.Message}");
            }
        }

        // Method to make player invisible to AI - improved implementation
        private static void ApplyRevivableStatePlayer(Player player)
        {
            try
            {
                string playerId = player.ProfileId;

                player.HandsController.IsAiming = false;
                player.MovementContext.EnableSprint(false);
                player.MovementContext.SetPoseLevel(0);
                player.MovementContext.IsInPronePose = true;
                player.ResetLookDirection();

                // stops player from moving etc hands from doing stuff
                // player.SetEmptyHands(null);
                
                player.ActiveHealthController.IsAlive = false;

            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogError($"Error applying stealth mode: {ex.Message}");
            }
        }

        public static KeyValuePair<string, bool> CheckRevivalItemInRaidInventory()
        {
            Plugin.LogSource.LogDebug("Checking for revival item in inventory");

            try
            {
                if (PlayerClient == null)
                {
                    if (Singleton<GameWorld>.Instantiated)
                    {
                        PlayerClient = Singleton<GameWorld>.Instance.MainPlayer;
                        Plugin.LogSource.LogDebug($"Initialized PlayerClient: {PlayerClient != null}");
                    }
                    else
                    {
                        Plugin.LogSource.LogWarning("GameWorld not instantiated yet");
                        return new KeyValuePair<string, bool>(string.Empty, false);
                    }
                }

                if (PlayerClient == null)
                {
                    Plugin.LogSource.LogError("PlayerClient is still null after initialization attempt");
                    return new KeyValuePair<string, bool>(string.Empty, false);
                }

                string playerId = PlayerClient.ProfileId;
                var inRaidItems = PlayerClient.Inventory.GetPlayerItems(EPlayerItems.Equipment);
                bool hasItem = inRaidItems.Any(item => item.TemplateId == Constants.Constants.ITEM_ID);

                Plugin.LogSource.LogDebug($"Player {playerId} has revival item: {hasItem}");
                return new KeyValuePair<string, bool>(playerId, hasItem);
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogError($"Error checking revival item: {ex.Message}");
                return new KeyValuePair<string, bool>(string.Empty, false);
            }
        }

        public static bool TryPerformManualRevival(Player player)
        {
            if (player == null)
                return false;

            string playerId = player.ProfileId;

            // Check if the player has the revival item
            bool hasDefib = CheckRevivalItemInRaidInventory().Value;

            // Check if the revival is on cooldown
            bool isOnCooldown = false;
            if (_lastRevivalTimesByPlayer.TryGetValue(playerId, out long lastRevivalTime))
            {
                long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                isOnCooldown = (currentTime - lastRevivalTime) < Settings.REVIVAL_COOLDOWN.Value;
            }

            if (isOnCooldown)
            {
                // Calculate remaining cooldown
                long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                int remainingCooldown = (int)(Settings.REVIVAL_COOLDOWN.Value - (currentTime - lastRevivalTime));

                NotificationManagerClass.DisplayMessageNotification(
                    $"Revival on cooldown! Available in {remainingCooldown} seconds",
                    ENotificationDurationType.Long,
                    ENotificationIconType.Alert,
                    Color.yellow);
                if (!Settings.TESTING.Value) return false;

            }

            if (hasDefib || Settings.TESTING.Value)
            {
                // Consume the item
                if (hasDefib && !Settings.TESTING.Value)
                {
                    ConsumeDefibItem(player);
                }

                // Apply revival effects - now with limited healing
                HealPlayer(player);

                // Apply invulnerability
                StartInvulnerability(player);

                player.ActiveHealthController.IsAlive = true;

                player.Say(EPhraseTrigger.OnMutter, false, 2f, ETagStatus.Combat, 100, true);

                // Reset critical state
                _playerInCriticalState[playerId] = false;

                // Set last revival time
                _lastRevivalTimesByPlayer[playerId] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                // Show successful revival notification
                NotificationManagerClass.DisplayMessageNotification(
                    "Defibrillator used successfully! You are temporarily invulnerable.",
                    ENotificationDurationType.Long,
                    ENotificationIconType.Default,
                    Color.green);

                Plugin.LogSource.LogInfo($"Manual revival performed for player {playerId}");
                return true;
            }
            else
            {
                NotificationManagerClass.DisplayMessageNotification(
                    "No defibrillator found! Unable to revive!",
                    ENotificationDurationType.Long,
                    ENotificationIconType.Alert,
                    Color.red);

                return false;
            }
        }

        private static void ConsumeDefibItem(Player player)
        {
            try
            {
                var inRaidItems = player.Inventory.GetPlayerItems(EPlayerItems.Equipment);
                Item defibItem = inRaidItems.FirstOrDefault(item => item.TemplateId == Constants.Constants.ITEM_ID);

                if (defibItem != null)
                {
                    // Use reflection to access the necessary methods to destroy the item
                    MethodInfo moveMethod = AccessTools.Method(typeof(InventoryController), "ThrowItem");
                    if (moveMethod != null)
                    {
                        // This will effectively discard the item
                        moveMethod.Invoke(player.InventoryController, new object[] { defibItem, false, null });
                        Plugin.LogSource.LogInfo($"Consumed defibrillator item {defibItem.Id}");
                    }
                    else
                    {
                        Plugin.LogSource.LogError("Could not find ThrowItem method");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogError($"Error consuming defib item: {ex.Message}");
            }
        }

        private static void HealPlayer(Player player)
        {
            try
            {
                // Modified to provide limited healing instead of full healing
                ActiveHealthController healthController = player.ActiveHealthController;
                if (healthController == null)
                {
                    Plugin.LogSource.LogError("Could not get ActiveHealthController");
                    return;
                }

                if (!Settings.HARDCORE_MODE.Value && Settings.RESTORE_DESTROYED_BODY_PARTS.Value) {
                    // Apply limited healing - enough to survive but not full health

                    foreach (EBodyPart bodyPart in Enum.GetValues(typeof(EBodyPart)))
                    {
                        Plugin.LogSource.LogDebug($"{bodyPart.ToString()} is on {healthController.GetBodyPartHealth(bodyPart).Current} health.");
                        if (healthController.GetBodyPartHealth(bodyPart).Current < 1) { 
                            healthController.FullRestoreBodyPart(bodyPart);
                            Plugin.LogSource.LogDebug($"Restored {bodyPart.ToString()}.");
                        }
                    }

                    // Remove negative effects
                    RemoveAllNegativeEffects(healthController);
                }

                // Apply painkillers effect
                //healthController.DoPainKiller();

                Plugin.LogSource.LogInfo("Applied limited revival effects to player");
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogError($"Error applying revival effects: {ex.Message}");
            }
        }

        private static void RemoveAllNegativeEffects(ActiveHealthController healthController)
        {
            try
            {
                MethodInfo removeNegativeEffectsMethod = AccessTools.Method(typeof(ActiveHealthController), "RemoveNegativeEffects");
                if (removeNegativeEffectsMethod != null)
                {
                    foreach (EBodyPart bodyPart in Enum.GetValues(typeof(EBodyPart)))
                    {
                        try
                        {
                            removeNegativeEffectsMethod.Invoke(healthController, new object[] { bodyPart });
                        }
                        catch { }
                    }
                    Plugin.LogSource.LogInfo("Removed all negative effects from player");
                }
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogError($"Error removing effects: {ex.Message}");
            }
        }

        private static void StartInvulnerability(Player player)
        {
            if (player == null) return;

            string playerId = player.ProfileId;
            _playerIsInvulnerable[playerId] = true;
            _playerInvulnerabilityTimers[playerId] = Settings.REVIVAL_DURATION.Value;

            // Show notification that invulnerability has started
            if (player.IsYourPlayer)
            {
                NotificationManagerClass.DisplayMessageNotification(
                    "Temporary invulnerability has started.",
                    ENotificationDurationType.Long,
                    ENotificationIconType.Default,
                    Color.white);
            }

            Plugin.LogSource.LogInfo($"Started invulnerability for player {playerId} for {Settings.REVIVAL_DURATION.Value} seconds");
        }

        private static void EndInvulnerability(Player player)
        {
            if (player == null)
                return;

            string playerId = player.ProfileId;
            _playerIsInvulnerable[playerId] = false;
            _playerInvulnerabilityTimers.Remove(playerId);

            // Show notification that invulnerability has ended
            if (player.IsYourPlayer)
            {
                NotificationManagerClass.DisplayMessageNotification(
                    "Temporary invulnerability has ended.",
                    ENotificationDurationType.Long,
                    ENotificationIconType.Default,
                    Color.white);
            }

            Plugin.LogSource.LogInfo($"Ended invulnerability for player {playerId}");
        }

        public static bool IsPlayerInvulnerable(string playerId)
        {
            return _playerIsInvulnerable.TryGetValue(playerId, out bool invulnerable) && invulnerable;
        }
    }
}