using BepInEx;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Reflection;
using RoR2.UI;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using UnityEngine.AddressableAssets.ResourceLocators;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Rebindables;
using System.Collections;

namespace CollectiveRewiring {
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Rebindables.Rebindables.PluginGUID)]
    public class CollectiveRewiring : BaseUnityPlugin {
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "pseudopulse";
        public const string PluginName = "CollectiveRewiring";
        public const string PluginVersion = "1.1.3";
        public static CollectiveRewiring instance;

        public static BepInEx.Logging.ManualLogSource ModLogger;
        internal static Dictionary<string, Func<string, string>> LanguageLoadMap = new();

        public void Awake() {
            // set logger
            ModLogger = Logger;
            instance = this;

            ConfigManager.HandleConfigAttributes(typeof(CollectiveRewiring).Assembly, Config);

            Operator.Initialize();
            SolusHeart.Initialize();
            WanderingChef.Initialize();
            NeutroniumWeight.Initialize();
            AspectRecipes.Initialize();

            On.RoR2.Language.LoadAllTokensFromFolder += (orig, self, output) => {
                orig(self, output);

                for (int i = 0; i < output.Count; i++) {
                    var kvp = output[i];

                    if (LanguageLoadMap.ContainsKey(kvp.Key)) {
                        output[i] = new KeyValuePair<string, string>(kvp.Key, LanguageLoadMap[kvp.Key](kvp.Value));
                    }
                }
            };
        }

        public static void RunCoro(IEnumerator coro) {
            instance.StartCoroutine(coro);
        }

        public static void Replace(string token, string match, string replace) {
            CollectiveRewiring.LanguageLoadMap.Add(token, (x) => x.Replace(match, replace));
        }

        // kept for later
        private void GenerateMap() {
            string[] keys = File.ReadAllLines(Assembly.GetExecutingAssembly().Location.Replace("CollectiveRewiring.dll", "addressables.txt"));
            string outputFilePath = Assembly.GetExecutingAssembly().Location.Replace("CollectiveRewiring.dll", "Assets.cs");
            Dictionary<Type, List<Dictionary<string, string>>> map = new();
            foreach (string key in keys) {
                var obj = Addressables.LoadAssetAsync<UnityEngine.Object>(key).WaitForCompletion();
                if (obj)
                {
                    Type type = obj.GetType();
                    if (type.IsAbstract) continue;

                    if (!map.ContainsKey(type))
                    {
                        ModLogger.LogError(type.ToString());
                        map.Add(type, new());
                    }

                    string kstr = obj.name;

                    map[type].Add(new Dictionary<string, string>() {
                        {key, kstr}
                    });
                }
                else
                {
                    Logger.LogError("no obj for key: " + key.ToString());
                }
            }
            

            StringBuilder builder = new();
            builder.AppendLine("using UnityEngine;");
            builder.AppendLine("using RoR2;");
            builder.AppendLine("using RoR2.Skills;");
            builder.AppendLine("using UnityEngine.AddressableAssets;");
            builder.AppendLine("using UnityEngine.Rendering.PostProcessing;");
            builder.AppendLine("");
            builder.AppendLine("namespace SecondStrike.Utils {");

            Regex rgx = new Regex("[^a-zA-Z0-9]");

            Dictionary<Type, List<string>> guh = new();

            foreach (KeyValuePair<Type, List<Dictionary<string, string>>> pair in map) {
                builder.AppendLine("    public static class " + pair.Key.Name +" {");

                if (!guh.ContainsKey(pair.Key)) {
                    guh.Add(pair.Key, new());
                }

                foreach (var item in pair.Value) {
                    foreach (var kvp in item) {
                        string name = kvp.Value;
                        name = rgx.Replace(name, "");

                        if (char.IsDigit(name[0])) {
                            name = "_" + name;
                        }

                        if (guh[pair.Key].Contains(name)) {
                            continue;
                        }

                        guh[pair.Key].Add(name);

                        builder.AppendLine($"       public static {pair.Key.ToString()} {name} => Addressables.LoadAssetAsync<{pair.Key.ToString()}>(\"{kvp.Key}\").WaitForCompletion();");
                    }
                }

                builder.AppendLine("    }");
            }

            builder.AppendLine("}");

            File.WriteAllText(outputFilePath, builder.ToString());
        }
    }
}