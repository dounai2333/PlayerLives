using EFT;
using EFT.HealthSystem;
using EFT.Communications;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using PlayerLives.Helpers;


namespace PlayerLives.Features
{
    /// <summary>
    /// Enhanced revival feature with manual activation and temporary invulnerability with restrictions
    /// </summary>
    internal class RevivalFeatures : ModulePatch
    {
        private static readonly bool DISABLE_SHOOTING_DURING_INVULNERABILITY = true; // Disable shooting during invulnerability
        // States
        private static Dictionary<string, long> _lastRevivalTimesByPlayer = new Dictionary<string, long>();
        private static Dictionary<string, bool> _playerInCriticalState = new Dictionary<string, bool>();
        private static Dictionary<string, bool> _playerIsInvulnerable = new Dictionary<string, bool>();
        private static Dictionary<string, float> _playerInvulnerabilityTimers = new Dictionary<string, float>();
        private static Dictionary<string, float> _playerDamageCoeff = new Dictionary<string, float>();
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
                if (!__instance.IsYourPlayer) return;

                // Update invulnerability timer if active
                if (_playerIsInvulnerable.TryGetValue(playerId, out bool isInvulnerable) && isInvulnerable)
                {
                    if (_playerInvulnerabilityTimers.TryGetValue(playerId, out float timer))
                    {
                        timer -= Time.deltaTime;
                        _playerInvulnerabilityTimers[playerId] = timer;

                        if (DISABLE_SHOOTING_DURING_INVULNERABILITY)
                        {
                            // disable shooting by setting hands to nothing
                            PlayerClient.SetEmptyHands(null);
                        }

                        // End invulnerability if timer is up
                        if (timer <= 0)
                        {
                            EndInvulnerability(__instance);
                        }
                    }
                }

                // Check for manual revival key press when in critical state
                if (_playerInCriticalState.TryGetValue(playerId, out bool inCritical) && inCritical)
                {

                    // Force critical state 
                    PlayerClient.SetEmptyHands(null);
                    PlayerClient.MovementContext.SetPoseLevel(0);
                    PlayerClient.MovementContext.IsInPronePose = true;

                    if (Input.GetKeyDown(Settings.REVIVAL_KEY.Value))
                    {
                        TryPerformManualRevival(__instance);
                    }

                    // Check for give up
                    if (Input.GetKeyDown(Settings.GIVE_UP_KEY.Value))
                    {
                        GiveUp(__instance);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogError($"Error in RevivalFeatureExtension patch: {ex.Message}");
            }
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

                // Put player in down state 
                ApplyCriticalStatePlayer(player);

                if (player.IsYourPlayer)
                {
                    try
                    {
                        // Show revival message
                        NotificationManagerClass.DisplayMessageNotification(
                            $"CRITICAL CONDITION! Press {Settings.REVIVAL_KEY.Value.ToString()} to revive!",
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

        private static void ApplyCriticalStatePlayer(Player player)
        {
            try
            {
                string playerId = player.ProfileId;

                // player.SetEmptyHands(null);
                player.HandsController.IsAiming = false;
                player.MovementContext.EnableSprint(false);
                player.MovementContext.SetPoseLevel(0);
                player.MovementContext.IsInPronePose = true;

                player.ResetLookDirection();
                player.MovementContext.ReleaseDoorIfInteractingWithOne();

                // Apply black out effect on revive
                player.ActiveHealthController.DoContusion(1f, 1f);
                player.ActiveHealthController.DoStun(1f, 1f);

                // Is alive player can't open menu
                // player.ActiveHealthController.IsAlive = false;

            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogError($"Error applying stealth mode: {ex.Message}");
            }
        }

        public static bool TryPerformManualRevival(Player player)
        {
            if (player == null) return false;

            string playerId = player.ProfileId;

            // Set alive first before applying effects
            // player.ActiveHealthController.IsAlive = true;

            HealPlayer(player);

            StartInvulnerability(player);

            // Stand player up 
            player.MovementContext.SetPoseLevel(1);
            player.MovementContext.IsInPronePose = false;
            player.MovementContext.EnableSprint(true);

            player.Say(EPhraseTrigger.OnMutter, false, 2f, ETagStatus.Combat, 100, true);

            _playerInCriticalState[playerId] = false;
            _lastRevivalTimesByPlayer[playerId] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Show successful revival notification
            NotificationManagerClass.DisplayMessageNotification(
                $"Invulnerability started - {Settings.REVIVAL_DURATION.Value}s! {Plugin.CurrentLives} lives left.",
                ENotificationDurationType.Long,
                ENotificationIconType.Default,
                Color.green);

            Plugin.LogSource.LogInfo($"Manual revival performed for player {playerId}");
            return true;
        }

        public static bool GiveUp(Player player)
        {
            if (player == null) return false;

            string playerId = player.ProfileId;

            // Set alive first before applying effects
            _playerInCriticalState[playerId] = false;
            _lastRevivalTimesByPlayer[playerId] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            Plugin.GaveUp = true;

            // Kill player by applying fatal damage to head
            if (player.ActiveHealthController != null)
            {
                player
                .ActiveHealthController
                .ApplyDamage(EBodyPart.Head, player.ActiveHealthController.GetBodyPartHealth(EBodyPart.Head).Maximum + 100f, new DamageInfoStruct());
            }

            // Show successful revival notification
            NotificationManagerClass.DisplayMessageNotification(
                $"You gave up!",
                ENotificationDurationType.Long,
                ENotificationIconType.Default,
                Color.red);

            Plugin.LogSource.LogInfo($"Player gave up {playerId}");
            return true;
        }

        public static void HealPlayer(Player player)
        {
            try
            {
                ActiveHealthController healthController = player.ActiveHealthController;
                if (healthController == null)
                {
                    Plugin.LogSource.LogError("Could not get ActiveHealthController");
                    return;
                }

                foreach (EBodyPart bodyPart in Enum.GetValues(typeof(EBodyPart)))
                {
                    if (healthController.GetBodyPartHealth(bodyPart).Current < 1)
                    {
                        // Remove bleed from destroyed body parts
                        healthController.method_16(bodyPart);

                        if (Settings.RESTORE_DESTROYED_BODY_PARTS.Value)
                        {
                            // from healthController.FullRestoreBodyPart(bodyPart);
                            // take health down to 25%
                            ActiveHealthController.BodyPartState bodyPartState = healthController.Dictionary_0[bodyPart];
                            bodyPartState.IsDestroyed = false;
                            float healingPercent = bodyPartState.Health.Maximum * (Settings.RESTORE_DESTROYED_BODY_PARTS_HEALING.Value / 100f);
                            bodyPartState.Health = new HealthValue(
                                healingPercent,
                                bodyPartState.Health.Maximum
                            );
                            healthController.method_43(bodyPart, EDamageType.Undefined);
                            healthController.method_35(bodyPart);
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogError($"Error applying revival effects: {ex.Message}");
            }
        }

        private static void StartInvulnerability(Player player)
        {
            if (player == null) return;

            string playerId = player.ProfileId;
            _playerIsInvulnerable[playerId] = true;
            _playerInvulnerabilityTimers[playerId] = Settings.REVIVAL_DURATION.Value;

            // Apply damage reduction buff
            ActiveHealthController healthController = player.ActiveHealthController;
            _playerDamageCoeff[player.ProfileId] = healthController.DamageCoeff;
            healthController.SetDamageCoeff(0f);

            // Apply pain killer buff so you can run on damaged legs
            healthController.AddEffect<PainKiller>(EBodyPart.Head, null, Settings.REVIVAL_DURATION.Value);

            Plugin.LogSource.LogInfo($"Started invulnerability for player {playerId} for {Settings.REVIVAL_DURATION.Value} seconds");
        }

        protected class PainKiller : ActiveHealthController.GClass2813, GInterface332, IEffect, GInterface306, GInterface308, GInterface304
        {
            public string ItemTemplateId { get; set; }
            public float MaxDuration { get; set; }
            public void UpdateWithSameOne(float strength)
            {
                float num = MaxDuration * strength;
                AddWorkTime(Mathf.Clamp(base.TimeLeft + num, 0f, MaxDuration), reset: true);
            }
            public void StoreValues(string templateId, float duration)
            {
                ItemTemplateId = templateId;
                MaxDuration = duration;
            }
        }

        private static void EndInvulnerability(Player player)
        {
            if (player == null) return;

            string playerId = player.ProfileId;
            _playerIsInvulnerable[playerId] = false;
            _playerInvulnerabilityTimers.Remove(playerId);

            // Restore damageCoeff
            if (_playerDamageCoeff.TryGetValue(playerId, out float damageCoeff))
                player.ActiveHealthController.SetDamageCoeff(damageCoeff);

            NotificationManagerClass.DisplayMessageNotification(
                "Invulnerability ended!",
                ENotificationDurationType.Long,
                ENotificationIconType.Alert,
                Color.white);

            Plugin.LogSource.LogInfo($"Ended invulnerability for player {playerId}");
        }

        public static bool IsPlayerInvulnerable(string playerId)
        {
            return _playerIsInvulnerable.TryGetValue(playerId, out bool invulnerable) && invulnerable;
        }
    }
}