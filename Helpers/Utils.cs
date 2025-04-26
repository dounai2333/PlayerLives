using Comfort.Common;
using SPT.Common.Http;
using System.Collections.Generic;
using EFT;

namespace RevivaRevivalLitelMod.Helpers
{
    internal class Utils
    {
        public static Player GetYourPlayer() {
            Player player = Singleton<GameWorld>.Instance.MainPlayer;
            if (player == null) return null;          
            if (!player.IsYourPlayer) return null;
            return player;
        }

    }
}
