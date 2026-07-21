using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace MinimapMod
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "aaron.minimapmod";
        public const string PluginName = "MinimapMod";
        public const string PluginVersion = "1.0.0";

        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;

            var harmony = new Harmony(PluginGuid);
            harmony.PatchAll();

            Log.LogInfo("MinimapMod charge.");
        }
    }

    // Ajoute le composant minimap au joueur local des qu'il spawn.
    [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
    public class PlayerControllerB_Connect_Patch
    {
        static void Postfix(PlayerControllerB __instance)
        {
            if (__instance == GameNetworkManager.Instance.localPlayerController)
            {
                if (__instance.GetComponent<MinimapManager>() == null)
                {
                    __instance.gameObject.AddComponent<MinimapManager>();
                }
            }
        }
    }
}
