using System;
using BepInEx;
using HarmonyLib;

namespace HookTest
{
    [BepInPlugin("user.hooktest", "HookTest", "1.0.0")]
    public class HookTestPlugin : BaseUnityPlugin
    {
        internal static BepInEx.Logging.ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            Logger.LogInfo("HookTest: step 1 - Awake ran");
            try
            {
                var harmony = new Harmony("user.hooktest");
                Logger.LogInfo("HookTest: step 2 - Harmony object created");
                harmony.PatchAll();
                Logger.LogInfo("HookTest: step 3 - PatchAll finished without error");
            }
            catch (Exception e)
            {
                Logger.LogError("HookTest: patch failed: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(HeroController), "Start")]
    internal static class HeroControllerStartPatch
    {
        private static bool done;

        [HarmonyPostfix]
        private static void Postfix()
        {
            if (done) return;
            done = true;
            HookTestPlugin.Log.LogInfo("HookTest: step 4 - HeroController.Start postfix FIRED (hooks work)");
        }
    }
}
