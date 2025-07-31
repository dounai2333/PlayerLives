using BepInEx.Configuration;
using UnityEngine;

namespace PlayerLives.Helpers
{
    internal class Settings
    {
        public static ConfigEntry<float> REVIVAL_DURATION;
        public static ConfigEntry<KeyCode> REVIVAL_KEY;
        public static ConfigEntry<bool> RESTORE_DESTROYED_BODY_PARTS;
        public static ConfigEntry<bool> TESTING;
        public static ConfigEntry<int> PLAYER_LIVES;
        public static void Init(ConfigFile config)
        {
            PLAYER_LIVES = config.Bind(
                "General",
                "Player Lives",
                1,
               "How many revives per raid."
            );
            REVIVAL_DURATION = config.Bind(
                "General",
                "Invulnerability Duration (s)",
                10f,
               "How long you are invulnerable for after revive."
            );
            REVIVAL_KEY = config.Bind(
                "General",
                "Revival Key",
                KeyCode.F5
            );

            RESTORE_DESTROYED_BODY_PARTS = config.Bind(
                "On Revive",
                "Restore destroyed body parts",
                true,
               "Blackened body parts are restored and healed for 25%"
            );

            TESTING = config.Bind(
                "Development",
                "Test Mode",
                false,
                new ConfigDescription("", null, new ConfigurationManagerAttributes { IsAdvanced = true })
            );
        }
    }
}
