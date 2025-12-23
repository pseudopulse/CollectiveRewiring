using System;
using System.Collections;
using System.Linq;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Newtonsoft.Json.Utilities;
using R2API;
using RoR2.ContentManagement;
using static RoR2.ContentManagement.ContentManager;

namespace CollectiveRewiring {
    [ConfigSection("Tweaks :: Aspect Recipes")]
    public static class AspectRecipes {
        [ConfigField("Ifrits Distinction", "Restores the recipe for Ifrits Distinction.", true)]
        public static bool Blazing;
        [ConfigField("Silence Between Two Strikes", "Restores the recipe for Silence Between Two Strikes.", true)]
        public static bool Overloading;
        [ConfigField("Her Biting Embrace", "Restores the recipe for Her Biting Embrace.", true)]
        public static bool Glacial;
        [ConfigField("His Reassurance", "Restores a modified recipe for His Reassurance.", true)]
        public static bool Mending;
        [ConfigField("Aurelionites Blessing", "Restores the recipe for Aurelionites Blessing.", true)]
        public static bool Gilded;
        [ConfigField("Spectral Circlet", "Restores the recipe for Spectral Circlet.", true)]
        public static bool Celestine;
        [ConfigField("Nkuhanas Retort", "Restores the recipe for Nkuhanas Retort.", true)]
        public static bool Malachite;
        [ConfigField("Shared Design", "Restores the recipe for Shared Design.", true)]
        public static bool Perfected;
        [ConfigField("His Spiteful Boon", "Restores a modified recipe for His Spiteful Boon.", true)]
        public static bool Twisted;
        private static AspectCraftsPack pack;
        public static void Initialize() {
            pack = new();
            pack.Initialize();

            ApplyRecipe(Paths.ItemDef.Behemoth, Paths.EquipmentDef.EliteFireEquipment, Blazing);
            ApplyRecipe(Paths.ItemDef.ShockNearby, Paths.EquipmentDef.EliteLightningEquipment, Overloading);
            ApplyRecipe(Paths.ItemDef.Icicle, Paths.EquipmentDef.EliteIceEquipment, Glacial);
            ApplyRecipe(Paths.ItemDef.ImmuneToDebuff, Paths.EquipmentDef.EliteEarthEquipment, Mending);
            ApplyRecipe(Paths.ItemDef.BoostAllStats, Paths.EquipmentDef.EliteAurelioniteEquipment, Gilded);
            ApplyRecipe(Paths.ItemDef.GhostOnKill, Paths.EquipmentDef.EliteHauntedEquipment, Celestine);
            ApplyRecipe(Paths.ItemDef.NovaOnHeal, Paths.EquipmentDef.ElitePoisonEquipment, Malachite);
            ApplyRecipe(Paths.ItemDef.LunarBadLuck, Paths.EquipmentDef.EliteLunarEquipment, Perfected);
            ApplyRecipe(Paths.ItemDef.OnLevelUpFreeUnlock, Paths.EquipmentDef.EliteBeadEquipment, Twisted);
        }

        public static void ApplyRecipe(ItemDef item, EquipmentDef result, bool enabled) {
            if (enabled) {
                CraftableDef def = ScriptableObject.CreateInstance<CraftableDef>();
                def.pickup = result;
                def.recipes = new Recipe[] {
                    new Recipe() {
                        ingredients = new RecipeIngredient[] {
                            new RecipeIngredient() { pickup = Paths.ItemDef.HeadHunter },
                            new RecipeIngredient() { pickup = item }
                        }
                    }
                };
                AspectCraftsPack.craftableDefs.Add(def);
            }
        }

        public class AspectCraftsPack : IContentPackProvider
        {
            internal ContentPack contentPack = new ContentPack();
            public static List<CraftableDef> craftableDefs = new List<CraftableDef>();
            public string identifier => "CR.AspectCrafts";

            public void Initialize()
            {
                ContentManager.collectContentPackProviders += ContentManager_collectContentpackProviders;
            }

            private void ContentManager_collectContentpackProviders(AddContentPackProviderDelegate addContentPackProvider)
            {
                addContentPackProvider(this);
            }

            public IEnumerator LoadStaticContentAsync(LoadStaticContentAsyncArgs args)
            {
                contentPack.identifier = identifier;
                contentPack.craftableDefs.Add(craftableDefs.ToArray());
                args.ReportProgress(1f);
                yield break;
            }

            public IEnumerator GenerateContentPackAsync(GetContentPackAsyncArgs args)
            {
                ContentPack.Copy(contentPack, args.output);
                args.ReportProgress(1f);
                yield break;
            }

            public IEnumerator FinalizeAsync(FinalizeAsyncArgs args)
            {
                args.ReportProgress(1f);
                yield break;
            }
        }
    }

    /*
    Slot1 : Wake of Vultures
    Slot2 : Growth Nectar
    Result = Aurelionite's Blessing

    Slot1 : Wake of Vultures
    Slot2 : Beads of Fealty
    Result = His Spiteful Boon

    Slot1 : Wake of Vultures
    Slot2 : Sentry Key
    Result = Of One Mind

    Slot1 : Wake of Vultures
    Slot2 : Interstellar Desk Plant
    Result = His Reassurance

    Slot1 : Wake of Vultures
    Slot2 : Happiest Mask
    Result = Spectral Circlet


    Slot1 : Wake of Vultures
    Slot2 : Purity
    Result = Shared Design

    Slot1 : Wake of Vultures
    Slot2 : N'kuhana's Opinion
    Result = N'kuhana's Retort
*/
}