using System;
using System.Reflection;
using BepInEx;

namespace HookTest
{
    [BepInPlugin("user.hooktest", "HookTest", "1.0.2")]
    public class HookTestPlugin : BaseUnityPlugin
    {
        internal static BepInEx.Logging.ManualLogSource Log;
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        private void Awake()
        {
            Log = Logger;
            Logger.LogInfo("HookTest v2: step 1 - Awake ran");
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

            foreach (ConstructorInfo c in harmony.GetConstructors(All))
                Logger.LogInfo("HookTest: ctor " + Describe(c));
            foreach (MethodInfo m in harmony.GetMethods(All | BindingFlags.DeclaredOnly))
                Logger.LogInfo("HookTest: method " + Describe(m));

            Type hook = FindType("BepInEx.Hook");
            if (hook != null)
            {
                foreach (MethodInfo m in hook.GetMethods(All | BindingFlags.DeclaredOnly))
                    Logger.LogInfo("HookTest: Hook method " + Describe(m));
            }

            TryPatch(harmony);
        }

        private void TryPatch(Type harmony)
        {
            object instance = null;
            ConstructorInfo ctor = harmony.GetConstructor(new Type[] { typeof(string) });
            if (ctor != null)
                instance = ctor.Invoke(new object[] { "user.hooktest" });
            else
            {
                try { instance = Activator.CreateInstance(harmony); } catch (Exception) { }
            }
            if (instance == null)
            {
                Logger.LogError("HookTest: could not create a Harmony instance");
                return;
            }

            MethodBase target = typeof(HeroController).GetMethod("Start", All);
            MethodInfo postfix = typeof(HeroStartPatch).GetMethod("Postfix", All);
            Logger.LogInfo("HookTest: target=" + (target != null) + " postfix=" + (postfix != null));
            if (target == null || postfix == null) return;

            foreach (MethodInfo m in harmony.GetMethods(All))
            {
                if (m.Name != "PatchPostfix") continue;
                ParameterInfo[] ps = m.GetParameters();
                Logger.LogInfo("HookTest: trying " + Describe(m));
                if (ps.Length == 2
                    && typeof(MethodBase).IsAssignableFrom(ps[0].ParameterType)
                    && typeof(MethodBase).IsAssignableFrom(ps[1].ParameterType))
                {
                    try
                    {
                        m.Invoke(instance, new object[] { target, postfix });
                        Logger.LogInfo("HookTest: step 3 - PatchPostfix call finished without error");
                    }
                    catch (Exception e)
                    {
                        Logger.LogError("HookTest: PatchPostfix call failed: " + e);
                    }
                }
            }
        }

        private static string Describe(MethodBase m)
        {
            string s = m.Name + "(";
            ParameterInfo[] ps = m.GetParameters();
            for (int i = 0; i < ps.Length; i++)
            {
                if (i > 0) s += ", ";
                s += ps[i].ParameterType.FullName + " " + ps[i].Name;
            }
            return s + ")";
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
