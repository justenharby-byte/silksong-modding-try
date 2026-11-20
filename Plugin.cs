using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using UnityEngine;

namespace HookTest
{
    [BepInPlugin("user.alphaomegacrest", "AlphaOmegaCrestMaker", "1.1.0")]
    public class HookTestPlugin : BaseUnityPlugin
    {
        internal static BepInEx.Logging.ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            Logger.LogInfo("AOCrest v5: Awake");
            try { RegisterHooks(); }
            catch (Exception e) { Logger.LogError("AOCrest: RegisterHooks failed: " + e); }
            try { gameObject.AddComponent<CrestMaker>(); }
            catch (Exception e) { Logger.LogError("AOCrest: AddComponent maker failed: " + e); }
            try { gameObject.AddComponent<CrestDiag>(); }
            catch (Exception e) { Logger.LogError("AOCrest: AddComponent diag failed: " + e); }
        }

        private void RegisterHooks()
        {
            Type harmony = null;
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { harmony = a.GetType("HarmonyLib.Harmony", false); } catch (Exception) { }
                if (harmony != null) break;
            }
            if (harmony == null) { Logger.LogError("AOCrest: Harmony not found"); return; }
            object h = Activator.CreateInstance(harmony);
            MethodInfo pp = harmony.GetMethod("PatchPostfix", CrestMaker.All);
            if (pp == null) { Logger.LogError("AOCrest: PatchPostfix not found"); return; }
            AddHook(h, pp, "ToolItemManager", "Awake", "AfterToolItemManagerAwake");
            AddHook(h, pp, "PlayerData", "SetupNewPlayerData", "AfterPlayerDataSetup");
            AddHook(h, pp, "PlayerData", "SetupExistingPlayerData", "AfterPlayerDataSetup");
        }

        private void AddHook(object h, MethodInfo pp, string typeName, string method, string postfixName)
        {
            try
            {
                Type t = CrestMaker.FindType(typeName);
                if (t == null) { Logger.LogError("AOCrest: type missing " + typeName); return; }
                MethodInfo target = null;
                foreach (MethodInfo m in t.GetMethods(CrestMaker.All | BindingFlags.DeclaredOnly))
                {
                    if (m.Name == method) { target = m; break; }
                }
                MethodInfo postfix = typeof(Hooks).GetMethod(postfixName, CrestMaker.All);
                if (target == null || postfix == null) { Logger.LogError("AOCrest: method missing " + typeName + "." + method); return; }
                pp.Invoke(h, new object[] { target, postfix });
                Logger.LogInfo("AOCrest: hooked " + typeName + "." + method);
            }
            catch (Exception e) { Logger.LogError("AOCrest: hook " + typeName + "." + method + " failed: " + e); }
        }
    }

    internal static class Hooks
    {
        private static void AfterToolItemManagerAwake() { CrestMaker.TryMake("ToolItemManager.Awake"); }
        private static void AfterPlayerDataSetup() { CrestMaker.EnsureSave("PlayerData setup"); }
    }

    public class CrestMaker : MonoBehaviour
    {
        internal const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        internal const string CrestName = "AlphaOmega";
        private const int SlotCount = 9;
        private static readonly string[] SpeedFields = { "attackDuration", "quickAttackSpeedMult", "attackRecoveryTime", "attackCooldownTime", "quickAttackCooldownTime", "canBind" };
        internal static bool Made;
        internal static bool SaveDone;

        internal static void Log(string s) { HookTestPlugin.Log.LogInfo("AOCrest: " + s); }

        private void Awake() { StartCoroutine(Fallback()); }

        private IEnumerator Fallback()
        {
            for (int i = 0; i < 150; i++)
            {
                yield return new WaitForSeconds(2f);
                if (!Made) TryMake("fallback");
                if (Made && !SaveDone) EnsureSave("fallback");
                if (Made && SaveDone) yield break;
            }
        }

        internal static Type FindType(string name)
        {
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (Type t in a.GetTypes())
                        if (t.Name == name) return t;
                }
                catch (Exception) { }
            }
            return null;
        }

        internal static FieldInfo FindField(Type t, string name)
        {
            while (t != null)
            {
                FieldInfo f = t.GetField(name, All | BindingFlags.DeclaredOnly);
                if (f != null) return f;
                t = t.BaseType;
            }
            return null;
        }

        internal static object GetF(object o, string name)
        {
            FieldInfo f = FindField(o.GetType(), name);
            return f == null ? null : f.GetValue(o);
        }

        internal static void SetF(object o, string name, object v)
        {
            FieldInfo f = FindField(o.GetType(), name);
            if (f != null) f.SetValue(o, v);
            else Log("field not found: " + name);
        }

        internal static object GetPlayerData()
        {
            Type pd = FindType("PlayerData");
            if (pd == null) return null;
            foreach (FieldInfo f in pd.GetFields(All))
                if (f.IsStatic && f.FieldType == pd) { object v = f.GetValue(null); if (v != null) return v; }
            foreach (PropertyInfo p in pd.GetProperties(All))
            {
                if (p.PropertyType != pd || p.GetIndexParameters().Length != 0) continue;
                MethodInfo g = p.GetGetMethod(true);
                if (g != null && g.IsStatic) { object v = g.Invoke(null, null); if (v != null) return v; }
            }
            return null;
        }

        internal static void EnsureSave(string why)
        {
            try
            {
                object pd = GetPlayerData();
                if (pd == null) return;
                object te = GetF(pd, "ToolEquips");
                if (te == null) return;
                Type tt = te.GetType();
                MethodInfo set = tt.GetMethod("SetData", All);
                if (set == null) { Log("SetData missing"); return; }

                bool has = false;
                MethodInfo names = tt.GetMethod("GetValidNames", All);
                if (names != null)
                {
                    IList l = names.Invoke(te, null) as IList;
                    if (l != null) foreach (object o in l) if ((string)o == CrestName) has = true;
                }
                if (has)
                {
                    MethodInfo get = tt.GetMethod("GetData", All);
                    if (get == null || get.GetParameters().Length != 1) { SaveDone = true; Log("save entry kept (" + why + ")"); return; }
                    object cur = get.Invoke(te, new object[] { CrestName });
                    IList slots = cur == null ? null : GetF(cur, "Slots") as IList;
                    if (slots != null && (slots.Count == 0 || slots.Count == SlotCount)) { SaveDone = true; Log("save ok, slots=" + slots.Count + " (" + why + ")"); return; }
                    Log("save slots mismatch (" + (slots == null ? -1 : slots.Count) + ") -> reset");
                }

                Type dt = set.GetParameters()[1].ParameterType;
                object data = Activator.CreateInstance(dt);
                SetF(data, "IsUnlocked", true);
                SetF(data, "DisplayNewIndicator", true);
                FieldInfo sf = FindField(dt, "Slots");
                if (sf != null)
                {
                    if (sf.FieldType.IsArray) sf.SetValue(data, Array.CreateInstance(sf.FieldType.GetElementType(), 0));
                    else sf.SetValue(data, Activator.CreateInstance(sf.FieldType));
                }
                set.Invoke(te, new object[] { CrestName, data });
                SaveDone = true;
                Log("save data created (" + why + ")");
            }
            catch (Exception e) { Log("EnsureSave failed: " + e.Message); }
        }

        internal static void TryMake(string why)
        {
            if (Made) return;
            try
            {
                Type crestType = FindType("ToolCrest");
                if (crestType == null) return;
                Made = Build(crestType, why);
            }
            catch (Exception e) { HookTestPlugin.Log.LogError("AOCrest: build failed: " + e); }
        }

        private static bool Build(Type crestType, string why)
        {
            UnityEngine.Object witch = null, wanderer = null;
            foreach (UnityEngine.Object c in Resources.FindObjectsOfTypeAll(crestType))
            {
                if (c.name == CrestName) { Log("crest already exists"); return true; }
                if (c.name == "Witch") witch = c;
                if (c.name == "Wanderer") wanderer = c;
            }
            if (witch == null || wanderer == null) return false;

            UnityEngine.Object clone = UnityEngine.Object.Instantiate(witch);
            clone.name = CrestName;
            clone.hideFlags = HideFlags.HideAndDontSave;
            SetF(clone, "cachedName", false);
            SetF(clone, "nameCache", null);

            UnityEngine.Object witchCfg = (UnityEngine.Object)GetF(witch, "heroConfig");
            UnityEngine.Object wandCfg = (UnityEngine.Object)GetF(wanderer, "heroConfig");
            UnityEngine.Object newCfg = UnityEngine.Object.Instantiate(witchCfg);
            newCfg.name = CrestName;
            newCfg.hideFlags = HideFlags.HideAndDontSave;
            foreach (string fn in SpeedFields)
            {
                FieldInfo f = FindField(newCfg.GetType(), fn);
                if (f == null) continue;
                f.SetValue(newCfg, f.GetValue(wandCfg));
            }
            SetF(clone, "heroConfig", newCfg);

            if (BuildSlots(clone, witch, wanderer) == 0) return false;

            try
            {
                object dn = GetF(clone, "displayName");
                SetF(dn, "Key", "The Alpha and the Omega");
                SetF(clone, "displayName", dn);
            }
            catch (Exception e) { Log("name change failed: " + e.Message); }

            Register(clone);
            Log("DONE - crest created via " + why);
            return true;
        }

        private static object Pick(List<object> protos, string type, string bind)
        {
            object typeOnly = null;
            foreach (object p in protos)
            {
                if (GetF(p, "Type").ToString() != type) continue;
                if (typeOnly == null) typeOnly = p;
                if (GetF(p, "AttackBinding").ToString() == bind) return p;
            }
            return typeOnly;
        }

        private static int BuildSlots(UnityEngine.Object clone, UnityEngine.Object witch, UnityEngine.Object wanderer)
        {
            Array wSlots = (Array)GetF(witch, "slots");
            Array pSlots = (Array)GetF(wanderer, "slots");
            Type et = wSlots.GetType().GetElementType();
            List<object> protos = new List<object>();
            foreach (object s in wSlots) protos.Add(s);
            foreach (object s in pSlots) protos.Add(s);

            string[] types = { "Skill", "Red", "Yellow", "Blue", "Red", "Blue", "Yellow", "Blue", "Yellow" };
            string[] binds = { "Neutral", "Up", "Down", "Down", "Down", "Down", "Down", "Down", "Down" };
            int n = types.Length;

            Vector2[] pos = new Vector2[n];
            pos[0] = new Vector2(0f, -0.1f);
            for (int k = 0; k < n - 1; k++)
            {
                float ang = (90f - k * 360f / (n - 1)) * Mathf.Deg2Rad;
                pos[k + 1] = new Vector2(2.3f * Mathf.Cos(ang), 2.3f * Mathf.Sin(ang));
            }

            Vector2[] dirs = { new Vector2(0, 1), new Vector2(0, -1), new Vector2(-1, 0), new Vector2(1, 0) };
            string[] navNames = { "NavUpIndex", "NavDownIndex", "NavLeftIndex", "NavRightIndex" };
            string[] fbNames = { "NavUpFallbackIndex", "NavDownFallbackIndex", "NavLeftFallbackIndex", "NavRightFallbackIndex" };

            Array ns = Array.CreateInstance(et, n);
            for (int i = 0; i < n; i++)
            {
                object proto = Pick(protos, types[i], binds[i]);
                if (proto == null) { Log("no slot prototype for " + types[i]); return 0; }
                object s = Activator.CreateInstance(et);
                foreach (FieldInfo f in et.GetFields(All))
                    if (!f.IsStatic) f.SetValue(s, f.GetValue(proto));

                SetF(s, "Position", pos[i]);
                SetF(s, "IsLocked", false);
                for (int d = 0; d < 4; d++)
                {
                    int best = -1;
                    float bestScore = float.MaxValue;
                    for (int j = 0; j < n; j++)
                    {
                        if (j == i) continue;
                        Vector2 v = pos[j] - pos[i];
                        if (v.magnitude < 0.01f) continue;
                        float cos = Vector2.Dot(v.normalized, dirs[d]);
                        if (cos <= 0.35f) continue;
                        float score = v.magnitude * (2f - cos);
                        if (score < bestScore) { bestScore = score; best = j; }
                    }
                    SetF(s, navNames[d], best);
                    SetF(s, fbNames[d], -1);
                }
                ns.SetValue(s, i);
            }
            SetF(clone, "slots", ns);
            return n;
        }

        private static void Register(UnityEngine.Object clone)
        {
            Type listType = FindType("ToolCrestList");
            if (listType == null) { Log("ToolCrestList type not found"); return; }
            foreach (UnityEngine.Object list in Resources.FindObjectsOfTypeAll(listType))
            {
                bool added = false;
                Type t = list.GetType();
                while (t != null && !added)
                {
                    foreach (FieldInfo f in t.GetFields(All | BindingFlags.DeclaredOnly))
                    {
                        if (f.IsStatic) continue;
                        object v = f.GetValue(list);
                        IList il = v as IList;
                        if (il == null) continue;
                        if (v is Array)
                        {
                            Array old = (Array)v;
                            Type el = old.GetType().GetElementType();
                            if (el.Name != "ToolCrest") continue;
                            Array bigger = Array.CreateInstance(el, old.Length + 1);
                            Array.Copy(old, bigger, old.Length);
                            bigger.SetValue(clone, old.Length);
                            f.SetValue(list, bigger);
                        }
                        else il.Add(clone);
                        added = true;
                        Log("added to crest list field " + f.Name);
                        break;
                    }
                    t = t.BaseType;
                }
                if (!added) Log("could not find a list field to add the crest to");
            }
        }
    }

    public class CrestDiag : MonoBehaviour
    {
        private const BindingFlags All = CrestMaker.All;
        private string lastState = "";

        private static void L(string s) { HookTestPlugin.Log.LogInfo("AODiag: " + s); }

        private void Awake() { StartCoroutine(Run()); }

        private static string Desc(object v)
        {
            if (v == null) return "null";
            UnityEngine.Object uo = v as UnityEngine.Object;
            if (uo != null) return v.GetType().Name + "'" + uo.name + "'";
            Type t = v.GetType();
            if (t.IsPrimitive || t.IsEnum || v is string) return v.ToString();
            return t.Name;
        }

        private static string CurrentCrestId()
        {
            object pd = CrestMaker.GetPlayerData();
            if (pd == null) return "no PlayerData";
            FieldInfo cf = CrestMaker.FindField(pd.GetType(), "CurrentCrestID");
            return cf == null ? "CurrentCrestID missing" : Desc(cf.GetValue(pd));
        }

        private IEnumerator Run()
        {
            Type hcType = CrestMaker.FindType("HeroController");
            if (hcType == null) { L("HeroController missing"); yield break; }

            for (int i = 0; i < 300; i++)
            {
                yield return new WaitForSeconds(3f);
                bool ready = false;
                try { ready = UnityEngine.Object.FindObjectOfType(hcType) != null && CrestMaker.Made; }
                catch (Exception) { }
                if (ready) break;
            }

            try { L("slots info: " + SlotInfo()); }
            catch (Exception e) { L("slot info failed: " + e.Message); }

            yield return new WaitForSeconds(25f);
            try { AutoEquip(); }
            catch (Exception e) { L("auto equip failed: " + e); }

            for (int i = 0; i < 400; i++)
            {
                yield return new WaitForSeconds(3f);
                try { Track(hcType); }
                catch (Exception e) { L("track failed: " + e.Message); yield break; }
            }
        }

        private static string SlotInfo()
        {
            Type crestType = CrestMaker.FindType("ToolCrest");
            foreach (UnityEngine.Object c in Resources.FindObjectsOfTypeAll(crestType))
            {
                if (c.name != CrestMaker.CrestName) continue;
                Array slots = (Array)CrestMaker.GetF(c, "slots");
                PropertyInfo p = crestType.GetProperty("SaveData", All);
                object sd = p == null ? null : p.GetValue(c, null);
                IList sl = sd == null ? null : CrestMaker.GetF(sd, "Slots") as IList;
                return "crest slots=" + slots.Length + " saveSlots=" + (sl == null ? -1 : sl.Count);
            }
            return "crest not found";
        }

        private void AutoEquip()
        {
            Type tim = CrestMaker.FindType("ToolItemManager");
            if (tim == null) { L("ToolItemManager missing"); return; }
            MethodInfo m = tim.GetMethod("SetEquippedCrest", All);
            if (m == null) { L("SetEquippedCrest missing"); return; }
            object target = null;
            if (!m.IsStatic)
            {
                target = UnityEngine.Object.FindObjectOfType(tim);
                if (target == null) { L("no ToolItemManager instance"); return; }
            }
            L("before equip: " + CurrentCrestId());
            m.Invoke(target, new object[] { CrestMaker.CrestName });
            L("after equip: " + CurrentCrestId());
        }

        private void Track(Type hcType)
        {
            object hc = UnityEngine.Object.FindObjectOfType(hcType);
            if (hc == null) return;
            string cfg = "";
            foreach (FieldInfo f in hcType.GetFields(All))
            {
                if (f.IsStatic) continue;
                if (f.FieldType.Name == "HeroControllerConfig") cfg += f.Name + "=" + Desc(f.GetValue(hc)) + " ";
                else if (f.Name == "attackDuration") cfg += "attackDuration=" + f.GetValue(hc) + " ";
            }
            string state = "crestId=" + CurrentCrestId() + " " + cfg;
            if (state != lastState) { lastState = state; L("state: " + state); }
        }
    }
}
