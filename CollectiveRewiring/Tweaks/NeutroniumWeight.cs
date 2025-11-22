using System;
using System.Linq;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2.Items;
using RoR2.Orbs;

namespace CollectiveRewiring {
    [ConfigSection("Tweaks :: Neutronium Weight")]
    public static class NeutroniumWeight {
        [ConfigField("Always Returns", "Makes Neutronium Weight always return to you without fail instead of dropping on the ground occassionally.", true)]
        private static bool AlwaysReturns;
        [ConfigField("Holder Damage Boost", "Hits that transfer the Neutronium Weight deal a percentage of bonus damage when the weight lands. Vanilla is 0. Requires Always Returns to be enabled.", 100f)]
        private static float HolderDamageBoost;
        [ConfigField("Holder Damage Stack", "Hits that transfer the Neutronium Weight deal a percentage of bonus damage when the weight lands, stacking value. Vanilla is 0. Requires Always Returns to be enabled.", 50f)]
        private static float HolderDamageBoostStack;
        //
        private static GameObject TransferDebuffOrbEffect;
        //
        // todo: fix nullref, make in-flight orbs or weights on enemies return to owner at the end of stage
        private static List<WeightInfo> InFlightWeights = new();
        private static List<NeutroniumWeightHolder> HeldWeights = new();
        private class WeightInfo {
            public CharacterBody owner;
            public int stack;
        }
        public static void Initialize() {
            if (AlwaysReturns) {
                On.RoR2.Orbs.ItemTransferOrbDroppable.OnArrival += DontDrop;
                On.RoR2.Items.TransferDebuffOnHitUtils.FireProjectile += RedirectToOrb;
                On.RoR2.Orbs.ItemTransferOrbDroppable.Begin += ReplaceOrbEffect;
                IL.RoR2.GlobalEventManager.OnCharacterDeath += OhMyGodGearbox;
                On.RoR2.Run.AdvanceStage += OnStageExit;
                List<ItemTag> tags = Paths.ItemDef.TransferDebuffOnHit.tags.ToList();
                tags.Add(ItemTag.AIBlacklist);
                Paths.ItemDef.TransferDebuffOnHit.tags = tags.ToArray();

                if (HolderDamageBoost > 0f) {
                    IL.RoR2.GlobalEventManager.ProcessHitEnemy += OverrideHitLogic;

                    CollectiveRewiring.Replace(
                        Paths.ItemDef.TransferDebuffOnHit.descriptionToken, "on hit.",
                        $"on hit, dealing <style=cIsDamage>{HolderDamageBoost}%</style> <style=cStack>(+{HolderDamageBoostStack}% per stack)</style> <style=cIsDamage>TOTAL damage</style> when it lands."
                    );
                }

                TransferDebuffOrbEffect = PrefabAPI.InstantiateClone(Paths.GameObject.ItemTransferOrbEffect, "TransferDebuffOrbEffect");
                Transform root = TransferDebuffOrbEffect.transform.Find("BillboardBase");
                root.gameObject.SetActive(false);
                var obj = GameObject.Instantiate(Paths.GameObject.TransferDebuffOnHitProjectileGhost, TransferDebuffOrbEffect.transform);
                obj.RemoveComponent<ProjectileGhostController>();
                obj.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                TransferDebuffOrbEffect.transform.Find("Trail Parent").gameObject.SetActive(false);
                ContentAddition.AddEffect(TransferDebuffOrbEffect);
            }
        }

        private static void OnStageExit(On.RoR2.Run.orig_AdvanceStage orig, Run self, SceneDef destinationStage)
        {
            foreach (PlayerCharacterMasterController pcmc in PlayerCharacterMasterController.instances) {
                CharacterMaster master = pcmc.master;
                CharacterBody body = master.GetBody();

                foreach (WeightInfo info in InFlightWeights) {
                    if (info.owner == body) {
                        master.inventory.GiveItemPermanent(DLC3Content.Items.TransferDebuffOnHit, info.stack);
                    }
                }

                foreach (NeutroniumWeightHolder weight in HeldWeights) {
                    if (weight.owner == body) {
                        master.inventory.GiveItemPermanent(DLC3Content.Items.TransferDebuffOnHit, weight.stacks);
                    }
                }
            }

            orig(self, destinationStage);
        }

        private static void OhMyGodGearbox(ILContext il)
        {
            ILCursor c = new(il);

            c.TryGotoNext(MoveType.After,
                x => x.MatchLdloc(3),
                x => x.MatchLdnull(),
                x => x.MatchCallOrCallvirt(typeof(TransferDebuffOnHitUtils), nameof(TransferDebuffOnHitUtils.FireProjectile))
            );
            c.Index--;
            c.Remove();
            c.Emit(OpCodes.Ldarg_1);
            c.EmitDelegate<Action<CharacterBody, GameObject, DamageReport>>((cb, junk, report) => {
                if (report.attacker && cb) {
                    TransferItemDamaging(cb.gameObject, report.attacker, default);
                }
            });
        }

        private static void ReplaceOrbEffect(On.RoR2.Orbs.ItemTransferOrbDroppable.orig_Begin orig, ItemTransferOrbDroppable self)
        {
            self.duration = self.travelDuration;
            if (self.target || self.orbEffectTargetObjectOverride)
            {
                EffectData effectData = new EffectData
                {
                    origin = self.origin,
                    genericFloat = self.duration,
                    genericUInt = Util.IntToUintPlusOne((int)self.itemIndex)
                };

                if (self.orbEffectTargetObjectOverride) {
                    effectData.SetNetworkedObjectReference(self.orbEffectTargetObjectOverride.gameObject);
                } else {
                    effectData.SetHurtBoxReference(self.target);
                }
                EffectManager.SpawnEffect(TransferDebuffOrbEffect, effectData, transmit: true);
            }
        }

        private static void OverrideHitLogic(ILContext il)
        {
            ILCursor c = new(il);

            c.TryGotoNext(MoveType.Before, x => x.MatchCallOrCallvirt(typeof(TransferDebuffOnHitUtils), nameof(TransferDebuffOnHitUtils.FireProjectile)));
            c.Remove();
            c.Emit(OpCodes.Ldarg, 1);
            c.EmitDelegate<Action<CharacterBody, GameObject, DamageInfo>>((cb, target, info) => {
                if (target.TryGetComponent<CharacterBody>(out var body)) {
                    if (cb.isPlayerControlled || body.isPlayerControlled) {
                        TransferItemDamaging(cb ? cb.gameObject : null, target, info);
                    }
                }
            });
        }

        private static void DontDrop(On.RoR2.Orbs.ItemTransferOrbDroppable.orig_OnArrival orig, RoR2.Orbs.ItemTransferOrbDroppable self)
        {
            if (self.target && self.target.healthComponent && self.target.healthComponent.body.inventory) {
                self.target.healthComponent.body.inventory.GiveItemPermanent(self.itemIndex, self.stack);
            } 
        }

        private static void RedirectToOrb(On.RoR2.Items.TransferDebuffOnHitUtils.orig_FireProjectile orig, CharacterBody sourceBody, GameObject target)
        {
            if (target) {
                TransferItemDamaging(sourceBody.gameObject, target, default);
            }
            else {
                orig(sourceBody, target);
            }
        }

        private static void TransferItemDamaging(GameObject source, GameObject destination, DamageInfo info = default) {
            if (!source || !destination) return;

            CharacterBody src = source.GetComponent<CharacterBody>();
            CharacterBody dest = destination.GetComponent<CharacterBody>();
            if (!src || !dest) return;

            CharacterMaster srcMaster = src.master;
            CharacterMaster destMaster = dest.master;
            if (!srcMaster || !destMaster) return;

            Inventory srcInv = srcMaster.inventory;
            Inventory destInv = destMaster.inventory;
            int itemCountEffective = srcInv.GetItemCountEffective(DLC3Content.Items.TransferDebuffOnHit);
            int stacks = itemCountEffective;

            NeutroniumWeightHolder holder = srcMaster.GetComponent<NeutroniumWeightHolder>();
            if (holder) {
                if (holder.owner == dest) {
                    stacks = holder.stacks;
                }
                else if (holder.owner) {
                    dest = holder.owner;
                    destMaster = holder.owner.master;
                    destInv = holder.owner.inventory;
                    stacks = holder.stacks;
                }
            }

            if (itemCountEffective > 0)
            {
                HealthComponent healthComponent = dest.healthComponent;
                if (healthComponent != null && healthComponent.alive)
                {
                    ItemTransferOrbDamaging orb = new ItemTransferOrbDamaging(dest.corePosition)
                    {
                        origin = src.corePosition,
                        target = dest.mainHurtBox,
                        itemIndex = DLC3Content.Items.TransferDebuffOnHit.itemIndex,
                        stack = stacks,
                        inventoryToGrantTo = destInv,
                        damage = info,
                        source = srcInv,
                        srcBody = src,
                    };
                    OrbManager.instance.AddOrb(orb);
                    srcInv.RemoveItemPermanent(DLC3Content.Items.TransferDebuffOnHit, itemCountEffective);
                }
            }
        }
        public class NeutroniumWeightHolder : MonoBehaviour {
            public CharacterBody owner;
            public int stacks;
            public CharacterMaster master;
            public void Start() {
                master = GetComponent<CharacterMaster>();
                HeldWeights.Add(this);
            }
            public void OnDestroy() {
                HeldWeights.Remove(this);
            }
        }

        public class ItemTransferOrbDamaging : ItemTransferOrbDroppable, IOrbFixedUpdateBehavior
        {
            public DamageInfo damage;
            public Inventory source;
            public CharacterBody srcBody;
            private WeightInfo info;
            private float speed = 60f;
            private Vector3 targetPos;
            public ItemTransferOrbDamaging(Vector3 targetStartingCorePosition) : base(targetStartingCorePosition)
            {
            }

            public override void Begin()
            {
                base.arrivalTime = Vector3.Distance(origin, target.transform.position) / speed;
                base.Begin();
                targetPos = target.transform.position;

                if (srcBody) {
                    info = new() {
                        owner = srcBody,
                        stack = stack
                    };

                    InFlightWeights.Add(info);
                }
            }

            public void FixedUpdate()
            {
                if (target) {
                    targetPos = target.transform.position;
                }
            }

            public override void OnArrival()
            {
                base.OnArrival();

                if (info != null) {
                    InFlightWeights.Remove(info);
                }

                if (target && target.healthComponent && target.healthComponent.alive) {
                    if (damage != null) {
                        DamageInfo info = damage;
                        info.damageColorIndex = DamageColorIndex.Electrocution;
                        info.damageType |= DamageType.CrippleOnHit;

                        target.healthComponent.TakeDamage(damage);
                        GlobalEventManager.instance.OnHitAll(damage, target.healthComponent.gameObject);
                        GlobalEventManager.instance.OnHitEnemy(damage, target.healthComponent.gameObject);
                    }

                    if (target.healthComponent.body.master) {
                        NeutroniumWeightHolder weight = GetOrAddComponent<NeutroniumWeightHolder>(target.healthComponent.body.master);
                        weight.owner = srcBody;
                        weight.stacks = stack;
                    }
                }
                else if (source && srcBody) {
                    ItemTransferOrbDamaging orb = new ItemTransferOrbDamaging(srcBody.corePosition)
                    {
                        origin = targetPos,
                        target = srcBody.mainHurtBox,
                        itemIndex = DLC3Content.Items.TransferDebuffOnHit.itemIndex,
                        stack = stack,
                        inventoryToGrantTo = source,
                        damage = default,
                    };
                    OrbManager.instance.AddOrb(orb);
                }
            }
        }

        private static T GetOrAddComponent<T>(Component comp) where T : Component {
            T t = comp.GetComponent<T>();

            return t ? t : comp.AddComponent<T>();
        }
    }
}