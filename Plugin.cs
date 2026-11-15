using System;
using System.Runtime.CompilerServices;
using BepInEx;

namespace HookTest
{
    [BepInPlugin("user.hooktest", "HookTest", "1.0.1")]
    public class HookTestPlugin : BaseUnityPlugin
    {
        internal static BepInEx.Logging.ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            Logger.LogInfo("HookTest: step 1 - Awake ran (no Harmony used yet)");
            try
            {
                ApplyPatches();
                Logger.LogInfo("HookTest: step 3 - ApplyPatches finished without error");
            }
            catch (Exception e)
            {
                Logger.LogError("HookTest: ApplyPatches failed: " + e);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ApplyPatches()
        {
            var harmony = new HarmonyLib.Harmony("user.hooktest");
            Logger.LogInfo("HookTest: step 2 - Harmony object created");
            harmony.PatchAll();
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(HeroController), "Start")]
    internal static class HeroControllerStartPatch
    {
        private static bool done;

        [HarmonyLib.HarmonyPostfix]
        private static void Postfix()
        {
            if (done) return;
            done = true;
            HookTestPlugin.Log.LogInfo("HookTest: step 4 - HeroController.Start postfix FIRED (hooks work)");
        }
    }
}
