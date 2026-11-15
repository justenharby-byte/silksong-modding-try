using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace HookTest
{
    public class CrestProbe : MonoBehaviour
    {
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        private static BepInEx.Logging.ManualLogSource Logger { get { return HookTestPlugin.Log; } }

        private void Awake()
        {
            Logger.LogInfo("CrestProbe: Awake");
            StartCoroutine(Run());
        }

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

        private bool Ready(Type crestType)
        {
            try
            {
                UnityEngine.Object[] all = Resources.FindObjectsOfTypeAll(crestType);
                Type hc = FindType("HeroController");
                return all.Length > 0 && hc != null && UnityEngine.Object.FindObjectOfType(hc) != null;
            }
            catch (Exception) { return false; }
        }

        private IEnumerator Run()
        {
            Type crestType = FindType("ToolCrest");
            if (crestType == null) { Logger.LogError("CrestProbe: ToolCrest type not found"); yield break; }
            for (int i = 0; i < 300; i++)
            {
                yield return new WaitForSeconds(2f);
                if (Ready(crestType)) break;
            }
            try { Dump(crestType); }
            catch (Exception e) { Logger.LogError("CrestProbe: dump failed: " + e); }
        }

        private void Dump(Type crestType)
        {
            UnityEngine.Object[] crests = Resources.FindObjectsOfTypeAll(crestType);
            Logger.LogInfo("CrestProbe: found crests = " + crests.Length);
            foreach (UnityEngine.Object c in crests)
            {
                string n = c.name;
                Logger.LogInfo("CrestProbe: crest " + n);
                if (n.IndexOf("Witch", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("Wanderer", StringComparison.OrdinalIgnoreCase) >= 0)
                    DumpObject(c, "  [" + n + "] ", 0);
            }

            Type tim = FindType("ToolItemManager");
            if (tim != null)
            {
                foreach (FieldInfo f in tim.GetFields(All))
                    if (f.Name.IndexOf("rest", StringComparison.OrdinalIgnoreCase) >= 0
                        || f.Name.IndexOf("nstance", StringComparison.OrdinalIgnoreCase) >= 0)
                        Logger.LogInfo("CrestProbe: ToolItemManager field " + (f.IsStatic ? "static " : "") + f.FieldType.Name + " " + f.Name);
            }
            Logger.LogInfo("CrestProbe: DONE");
        }

        private void DumpObject(object o, string prefix, int depth)
        {
            if (o == null) return;
            foreach (FieldInfo f in o.GetType().GetFields(All))
            {
                if (f.IsStatic) continue;
                object v;
                try { v = f.GetValue(o); } catch (Exception) { continue; }
                string name = prefix + f.Name;
                if (v == null) { Logger.LogInfo("CrestProbe: " + name + " = null"); continue; }
                Type t = v.GetType();
                if (t.IsPrimitive || t.IsEnum || v is string)
                    Logger.LogInfo("CrestProbe: " + name + " = " + v);
                else if (v is UnityEngine.Object)
                    Logger.LogInfo("CrestProbe: " + name + " -> " + t.Name + " '" + ((UnityEngine.Object)v).name + "'");
                else if (v is IEnumerable)
                {
                    int count = 0;
                    foreach (object item in (IEnumerable)v)
                    {
                        if (count < 12 && depth < 2) DumpObject(item, name + "[" + count + "]. ", depth + 1);
                        count++;
                    }
                    Logger.LogInfo("CrestProbe: " + name + " count = " + count);
                }
                else if (depth < 2)
                    DumpObject(v, name + ". ", depth + 1);
                else
                    Logger.LogInfo("CrestProbe: " + name + " : " + t.Name);
            }
        }
    }
}
