using System;
using System.Reflection;
using BepInEx;

namespace HookTest
{
    [BepInPlugin("user.hooktest", "HookTest", "1.0.4")]
    public class HookTestPlugin : BaseUnityPlugin
    {
        internal static BepInEx.Logging.ManualLogSource Log;
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        private void Awake()
        {
            Log = Logger;
            Logger.LogInfo("HookTest v4: step 1 - Awake ran");

            try { gameObject.AddComponent<CrestProbe>(); }
            catch (Exception e) { Logger.LogError("HookTest: AddComponent failed: " + e); }

            try { Probe(); }
            catch (Exception e) { Logger.LogError("HookTest: Probe failed: " + e); }
        }

        private void Probe()
        {
            Type harmony = FindType("HarmonyLib.Harmony");
            if (harmony == null)
            {
                Logger.LogError("HookTest: HarmonyLib.Harmony not found in any loaded assembly");
                return;
            }
            Logger.LogInfo("HookTest: step 2 - found Harmony in assembly: " + harmony.Assembly.GetName().Name);

            object instance = Activator.CreateInstance(harmony);
            MethodBase target = typeof(HeroController).GetMethod("Start", All);
            MethodInfo postfix = typeof(HeroStartPatch).GetMethod("Postfix", All);
            Logger.LogInfo("HookTest: target=" + (target != null) + " postfix=" + (postfix != null));
            if (target == null || postfix == null) return;

            MethodInfo patchPostfix = harmony.GetMethod("PatchPostfix", All);
            if (patchPostfix == null)
            {
                Logger.LogError("HookTest: PatchPostfix not found");
                return;
            }
            patchPostfix.Invoke(instance, new object[] { target, postfix });
            Logger.LogInfo("HookTest: step 3 - PatchPostfix call finished without error");
        }

        private static Type FindType(string name)
        {
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type t = a.GetType(name, false);
                    if (t != null) return t;
                }
                catch (Exception) { }
            }
            return null;
        }
    }

    internal static class HeroStartPatch
    {
        private static bool done;

        private static void Postfix()
        {
            if (done) return;
            done = true;
            HookTestPlugin.Log.LogInfo("HookTest: step 4 - HeroController.Start postfix FIRED (hooks work)");
        }
    }
}
