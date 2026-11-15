using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using UnityEngine;

namespace HookTest
{
    [BepInPlugin("user.alphaomegacrest", "AlphaOmegaCrestMaker", "1.0.0")]
    public class HookTestPlugin : BaseUnityPlugin
    {
        internal static BepInEx.Logging.ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            Logger.LogInfo("AOCrest v1: Awake");
            try { gameObject.AddComponent<CrestMaker>(); }
            catch (Exception e) { Logger.LogError("AOCrest: AddComponent failed: " + e); }
        }
    }

    public class CrestMaker : MonoBehaviour
    {
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private const string CrestName = "AlphaOmega";
        private static readonly string[] SpeedFields = { "attackDuration", "quickAttackSpeedMult", "attackRecoveryTime", "attackCooldownTime", "quickAttackCooldownTime", "canBind" };

        private static void Log(string s) { HookTestPlugin.Log.LogInfo("AOCrest: " + s); }

        private void Awake() { StartCoroutine(Run()); }

        private static Type FindType(string name)
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

        private static FieldInfo FindField(Type t, string name)
        {
            while (t != null)
            {
                FieldInfo f = t.GetField(name, All | BindingFlags.DeclaredOnly);
                if (f != null) return f;
                t = t.BaseType;
            }
            return null;
        }

        private static object GetF(object o, string name)
        {
            FieldInfo f = FindField(o.GetType(), name);
            return f == null ? null : f.GetValue(o);
        }

        private static void SetF(object o, string name, object v)
        {
            FieldInfo f = FindField(o.GetType(), name);
            if (f != null) f.SetValue(o, v);
            else Log("field not found: " + name);
        }

        private bool Ready(Type crestType, Type hcType)
        {
            try
            {
                return Resources.FindObjectsOfTypeAll(crestType).Length >= 5
                    && UnityEngine.Object.FindObjectOfType(hcType) != null;
            }
            catch (Exception) { return false; }
        }

        private IEnumerator Run()
        {
            Type crestType = FindType("ToolCrest");
            Type hcType = FindType("HeroController");
            if (crestType == null || hcType == null) { Log("types missing"); yield break; }
            for (int i = 0; i < 300; i++)
            {
                yield return new WaitForSeconds(2f);
                if (Ready(crestType, hcType)) break;
            }
            try { Build(crestType); }
            catch (Exception e) { HookTestPlugin.Log.LogError("AOCrest: build failed: " + e); }
        }

        private void Build(Type crestType)
        {
            UnityEngine.Object witch = null, wanderer = null;
            foreach (UnityEngine.Object c in Resources.FindObjectsOfTypeAll(crestType))
            {
                if (c.name == CrestName) { Log("crest already exists"); return; }
                if (c.name == "Witch") witch = c;
                if (c.name == "Wanderer") wanderer = c;
            }
            if (witch == null || wanderer == null) { Log("Witch or Wanderer not found"); return; }

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
                if (f == null) { Log("config field missing: " + fn); continue; }
                object before = f.GetValue(newCfg);
                object wand = f.GetValue(wandCfg);
                f.SetValue(newCfg, wand);
                Log("config " + fn + ": Witch=" + before + " Wanderer=" + wand + " -> now " + f.GetValue(newCfg));
            }
            SetF(clone, "heroConfig", newCfg);

            BuildSlots(clone, witch, wanderer);

            try
            {
                object dn = GetF(clone, "displayName");
                SetF(dn, "Key", "The Alpha and the Omega");
                SetF(clone, "displayName", dn);
            }
            catch (Exception e) { Log("name change failed: " + e.Message); }

            Register(clone);

            try
            {
                MethodInfo unlock = crestType.GetMethod("Unlock", All);
                if (unlock != null && unlock.GetParameters().Length == 0)
                {
                    unlock.Invoke(clone, null);
                    Log("Unlock called");
                }
                else Log("Unlock not callable without arguments");
            }
            catch (Exception e) { Log("Unlock failed: " + e.Message); }

            Log("DONE - crest " + CrestName + " created");
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

        private void BuildSlots(UnityEngine.Object clone, UnityEngine.Object witch, UnityEngine.Object wanderer)
        {
            Array wSlots = (Array)GetF(witch, "slots");
            Array pSlots = (Array)GetF(wanderer, "slots");
            Type et = wSlots.GetType().GetElementType();
            List<object> protos = new List<object>();
            foreach (object s in wSlots) protos.Add(s);
            foreach (object s in pSlots) protos.Add(s);

            string[] types = { "Skill", "Red", "Yellow", "Blue", "Red", "Blue", "Yellow", "Blue" };
            string[] binds = { "Neutral", "Up", "Down", "Down", "Down", "Down", "Down", "Down" };
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
                if (proto == null) { Log("no slot prototype for " + types[i]); return; }
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
            Log("slots built: " + n);
        }

        private void Register(UnityEngine.Object clone)
        {
            Type listType = FindType("ToolCrestList");
            if (listType == null) { Log("ToolCrestList type not found"); return; }
            UnityEngine.Object[] lists = Resources.FindObjectsOfTypeAll(listType);
            Log("ToolCrestList assets: " + lists.Length);
            foreach (UnityEngine.Object list in lists)
            {
                bool added = false;
                Type t = list.GetType();
                while (t != null && !added)
                {
                    foreach (FieldInfo f in t.GetFields(All | BindingFlags.DeclaredOnly))
                    {
                        if (f.IsStatic) continue;
                        Log("list field " + f.FieldType.Name + " " + f.Name);
                        object v = f.GetValue(list);
                        if (v is Array)
                        {
                            Array old = (Array)v;
                            Type el = old.GetType().GetElementType();
                            if (el.Name != "ToolCrest") continue;
                            Array bigger = Array.CreateInstance(el, old.Length + 1);
                            Array.Copy(old, bigger, old.Length);
                            bigger.SetValue(clone, old.Length);
                            f.SetValue(list, bigger);
                            added = true;
                            Log("added to array field " + f.Name);
                            break;
                        }
                        IList il = v as IList;
                        if (il != null)
                        {
                            il.Add(clone);
                            added = true;
                            Log("added to list field " + f.Name);
                            break;
                        }
                    }
                    t = t.BaseType;
                }
                if (!added) Log("could not find a list field to add the crest to");
            }
        }
    }
}
