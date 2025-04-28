using BepInEx;
using BepInEx.Logging;
using RevivalLite.Patches;
using RevivalLite.Helpers;
using RevivalLite.Features;

namespace RevivalLite
{
    [BepInPlugin("somtam.revivalLight", "Revival Lite", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource LogSource;

        private void Awake()
        {
            // save the Logger to variable so we can use it elsewhere in the project
            LogSource = Logger;
            LogSource.LogInfo("Revival plugin loaded!");
            Settings.Init(Config);

            // Enable patches
            new DeathPatch().Enable();
            new RevivalFeatures().Enable();
        }

    }
}