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
        public static ConfigEntry<string> REQUIRE_BUFF_TYPE;
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

            REQUIRE_BUFF_TYPE = config.Bind(
                "General",
                "Require Buff To Revive",
                "None",
                new ConfigDescription(
                    "Select required buff to be active for revive.",
                    new AcceptableValueList<string>("None", "BuffsAdrenaline", "BuffsPropital",
                    "BuffsSJ1TGLabs", "BuffsSJ6TGLabs", "BuffsZagustin", "BuffseTGchange",
                    "Buffs_2A2bTG", "Buffs_3bTG", "Buffs_AHF1M", "Buffs_Antidote",
                    "Buffs_L1", "Buffs_MULE", "Buffs_Meldonin", "Buffs_Obdolbos", "Buffs_Obdolbos2",
                    "Buffs_P22", "Buffs_PNB", "Buffs_Perfotoran", "Buffs_SJ12_TGLabs", "Buffs_Trimadol")
                )
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
