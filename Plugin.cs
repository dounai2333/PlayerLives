using BepInEx;
using BepInEx.Logging;
using PlayerLives.Patches;
using PlayerLives.Helpers;
using PlayerLives.Features;

namespace PlayerLives
{
    [BepInPlugin("com.somtam.playerLives", "Player Lives", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource LogSource;
        public static int CurrentLives;

        private void Awake()
        {
            // save the Logger to variable so we can use it elsewhere in the project
            LogSource = Logger;
            LogSource.LogInfo("Player Lives plugin loaded!");
            Settings.Init(Config);

            // Enable patches
            new DeathPatch().Enable();
            new RevivalFeatures().Enable();
            new RaidStartPatch().Enable();
        }

    }
}