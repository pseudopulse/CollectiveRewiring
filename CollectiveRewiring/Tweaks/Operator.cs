using System;
using System.Linq;
using EntityStates.Drone;
using EntityStates.Drone.Command;
using HG;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using Rebindables;
using RoR2BepInExPack.Utilities;
using UnityEngine.UI;

namespace CollectiveRewiring {
    [ConfigSection("Tweaks :: Operator")]
    public static class Operator {
        public static Sprite BackdropSprite;
        [ConfigField("Follower Attacks Enemies", "Allows the currently selected drone to still attack enemies.", true)]
        private static bool FollowerAttacks;
        [ConfigField("Allow Drone Cycling", "Lets you target select a drone via a rebindable key or cycle the queue without activating anything.", true)]
        private static bool AllowDroneCycling;
        [ConfigField("No Drone Grab", "If Allow Drone Cycling is on, disables the functionality of targeting a drone.", false)]
        private static bool NoGrab;
        [ConfigField("Drone Command ICD", "Adds a per-drone cooldown to commands. Drones on cooldown will not be valid in the queue and cannot be target grabbed. Cooldown is equal to your M2 cd. Recommended for balance when using target grabbing.", true)]
        private static bool UseDroneICD;
        [ConfigField("Better Queue UI", "Improves the Drone Queue UI.", true)]
        private static bool BetterQueueUI;
        [ConfigField("Ally Targeting", "Allows supportive drones to target allies.", true)]
        private static bool AllyTargeting;
        [ConfigField("DOC Stim Shot Duration", "The duration of the stat buff from DOC's stim shot. Vanilla is 4", 8f)]
        private static float DOCShotDur;
        [ConfigField("DOC Stim Shot Barrier", "The barrier gain from DOC's stim shot. Vanilla is 25.", 40)]
        private static float DOCBarrier;
        [ConfigField("Gunner Drone Barrage Damage", "The damage of Gunner Drone target barrages. Vanilla is 85. This does not apply to CROSSHAIRS.", 150)]
        private static float GDDamage;
        [ConfigField("Extra Ricochet Sound", "Adds an extra sound when you land a kill with M1.", true)]
        private static bool KillSound;
        [ConfigField("M2 Requires Press", "Makes default M2 not magdump in half a second when you hold the button down.", true)]
        private static bool RequirePress;
        [ConfigField("OCR Damage", "The damage of Operator's M1 shot. Vanilla is 600", 1000)]
        private static float NanoPistolDamage;
        [ConfigField("Nanobugged Damage Increase", "The damage increase against Nanobugged enemies. Vanilla is 100", 50)]
        private static float NanoBugDamage;
        [ConfigField("Unique Drones Copy Items", "Makes Operator's unique drones copy most items.", true)]
        private static bool CopyItems;
        //
        public static ModKeybind DroneCycle = RebindAPI.RegisterModKeybind(new("OPERATOR_CYCLE".Add("Drone Queue Cycle (Operator)"), KeyCode.F, 10));
        public static void Initialize() {
            if (FollowerAttacks) {
                On.EntityStates.Drone.Follow.FixedUpdate += (orig, self) => {
                    orig(self);

                    if (self.inputBank.skill1.down) {
                        self.skillLocator.primary?.ExecuteIfReady();
                    }

                    if (self.inputBank.skill2.down) {
                        self.skillLocator.secondary?.ExecuteIfReady();
                    }

                    if (self.inputBank.skill3.down) {
                        self.skillLocator.utility?.ExecuteIfReady();
                    }

                    if (self.inputBank.skill4.down) {
                        self.skillLocator.special?.ExecuteIfReady();
                    }
                };

                IL.EntityStates.Drone.Follow.Update += (il) => {
                    ILCursor c = new(il);
                    
                    c.TryGotoNext(MoveType.Before, // forward
                        x => x.MatchLdarg(0),
                        x => x.MatchCallOrCallvirt(out _),
                        x => x.MatchLdloc(3),
                        x => x.MatchCallOrCallvirt(out _)
                    );
                    for (int i = 0; i < 4; i++) {
                        c.Next.OpCode = OpCodes.Nop;
                        c.Index++;
                    }

                    c.TryGotoNext(MoveType.Before, // rotation
                        x => x.MatchLdarg(0),
                        x => x.MatchCallOrCallvirt(out _),
                        x => x.MatchLdloc(9),
                        x => x.MatchCallOrCallvirt(out _)
                    );
                    for (int i = 0; i < 4; i++) {
                        c.Next.OpCode = OpCodes.Nop;
                        c.Index++;
                    }

                    c.TryGotoNext(MoveType.Before, // aim vec
                        x => x.MatchLdarg(0),
                        x => x.MatchCallOrCallvirt(out _),
                        x => x.MatchLdloc(0),
                        x => x.MatchCallOrCallvirt(out _)
                    );
                    for (int i = 0; i < 4; i++) {
                        c.Next.OpCode = OpCodes.Nop;
                        c.Index++;
                    }

                    c.Emit(OpCodes.Ldarg_0);
                    c.EmitDelegate<Action<Follow>>((self) => {
                        FollowData data = FollowMap.GetOrCreateValue(self);
                        data.Setup(self.targetBody, self.characterBody);

                        if (data.stopwatch >= 0f)
                        {
                            data.stopwatch -= Time.deltaTime;
                        }

                        Vector3 idleDir = (self.followAim ? self.targetBody.inputBank.aimDirection : (self.targetBody.characterDirection ? self.targetBody.characterDirection.forward : self.targetBody.transform.forward));
                        idleDir.y = Mathf.Clamp(idleDir.y, -0.1f, 0.1f);
                        idleDir = idleDir.normalized;
                        Vector3 aimDir = self.inputBank.aimDirection.normalized;

                        Vector3 aimVec = self.characterDirection ? self.characterDirection.forward : self.transform.forward;

                        if (self.characterBody.master?.aiComponents?.Length > 0 && self.characterBody.master?.aiComponents[0]?.currentEnemy.gameObject && data.stopwatch != 0f)
                        {
                            aimVec = Vector3.SmoothDamp(aimVec, aimDir, ref self.velocityAim, self.aimDampTime);
                        }
                        else
                        {
                            aimVec = Vector3.SmoothDamp(aimVec, idleDir, ref self.velocityAim, self.aimDampTime);
                            self.inputBank.aimDirection = self.targetBody.inputBank.aimDirection;
                        }

                        if (self.characterDirection)
                        {
                            self.characterDirection.forward = aimVec;
                        }
                        else
                        {
                            self.transform.forward = aimVec;
                        }
                    });
                };
            }

            if (AllowDroneCycling) {
                On.DroneTechController.Start += (orig, self) => {
                    orig(self);
                    
                    if (self.hasAuthority) {
                        self.AddComponent<OperatorTracker>();
                    }
                };
            }
            
            if (UseDroneICD) {
                On.RoR2.Skills.DroneTechDroneSkillDef.IsReady += IsReady;
                On.RoR2.DroneCommandReceiver.IsReady += InternalCD;
                On.RoR2.DroneCommandReceiver.FixedUpdate += Recharge;
                On.EntityStates.DroneTech.Weapon.Activate.OnEnter += OnDroneCommand;
            }
            On.DroneTechSurvivorUIController.FillSecondaryQueue += UpdateIcons;
            On.DroneTechController.FixedUpdate += DroneTechUpdate;

            if (AllyTargeting) {
                On.RoR2.DroneCommandReceiver.DoTargetInfoSearch += AllowAllyTarget;
                On.RoR2.DroneCommandReceiver.Awake += AllowAllyGrab;
                Paths.SkillDef.CommandHealNova.activationState = new(typeof(HealNovaAssumePosition));
                Paths.SkillDef.CommandHealNova.activationStateMachineName = "Body";
                ContentAddition.AddEntityState<HealNovaAssumePosition>(out _);
                EntityStateConfiguration config = GameObject.Instantiate(Paths.EntityStateConfiguration.BombardmentAssumePositionEntityState);
                config.targetType = (SerializableSystemType)typeof(HealNovaAssumePosition);
                ContentAddition.AddEntityStateConfiguration(config);

                On.EntityStates.Drone.Command.CommandHealNovaPulse.OnEnter += (orig, self) => { // nerf radius on emergency drone m2
                    orig(self);
                    CommandHealNovaPulse.radius = 18f;
                };
            }

            On.EntityStates.Drone.Command.CommandStimShot.FireOrb += (orig, self) => { // buff doc m2
                self.healAmount = DOCBarrier * 0.01f;
                orig(self);
            };
            IL.EntityStates.Drone.Command.CommandStimShot.StimShotOrb.OnArrival += (il) => {
                ILCursor c = new(il);
                c.TryGotoNext(MoveType.Before, x => x.MatchLdcR4(4f));
                c.Next.Operand = DOCShotDur;
            };

            On.EntityStates.Drone.Command.CommandFireTurret.OnEnter += (orig, self) => { // buff gunner drone m2
                if (!self.shouldBounce) {
                    self.damageCoefficient = GDDamage * 0.01f;
                }
                orig(self);
            };
            Replace(Paths.SkillDef.CommandFireTurretSkillDef, "85%", $"{GDDamage}%");

            if (KillSound) {
                Paths.GameObject.NanoPistolKillEffect.AddComponent<RicochetSound>();
            }

            if (RequirePress) {
                Paths.DroneTechDroneSkillDef.Command.mustKeyPress = true;
            }

            Replace(Paths.SkillDef.FireNanoPistol, "600%", $"{NanoPistolDamage}%");

            On.EntityStates.DroneTech.Weapon.FireNanoPistol.ModifyBullet += (orig, self, x) => {
                self.maxDamageCoefficient = NanoPistolDamage * 0.01f;
                orig(self, x);
            };

            IL.RoR2.NanoBugDebuffController.OnVictimDamaged += ReduceNanoBugDamage;
            Replace(Paths.SkillDef.NanoBomb.keywordTokens[0], "100%", $"{NanoBugDamage}%");

            if (CopyItems) {
                On.RoR2.DroneRepairMaster.Start += (orig, self) => {
                    orig(self);
                    
                    if (!self.GetComponent<CopyOwnerInventory>()) {
                        self.AddComponent<CopyOwnerInventory>();
                    }
                };

                Replace("DRONETECH_PASSIVE_DESCRIPTION", "companions.", "companions that <style=cIsUtility>inherit your items.</style>");
            }
        }

        private static void OnDroneCommand(On.EntityStates.DroneTech.Weapon.Activate.orig_OnEnter orig, EntityStates.DroneTech.Weapon.Activate self)
        {
            orig(self);

            if (self.drone != null && self.drone.characterBody) {
                DroneSkillData data = SkillMap.GetOrCreateValue(self.drone.characterBody.gameObject);
                data.cooldown = 0f;
                data.finalRecharge = self.skillLocator.secondary.finalRechargeInterval;
            }            
        }

        private class CopyOwnerInventory : MonoBehaviour {
            public Inventory ownerInventory;
            public CharacterMaster self;
            private ItemTag[] blacklistedTags = new ItemTag[] {
                ItemTag.AIBlacklist, ItemTag.HoldoutZoneRelated, ItemTag.InteractableRelated, ItemTag.OnStageBeginEffect, ItemTag.PowerShape, ItemTag.DevotionBlacklist, ItemTag.ObjectiveRelated, ItemTag.ObliterationRelated, ItemTag.CannotCopy
            };

            public void Start() {
                self = GetComponent<CharacterMaster>();
            }

            public void FixedUpdate() {
                if (!ownerInventory) {
                    if (self.minionOwnership && self.minionOwnership.ownerMaster) {
                        ownerInventory = self.minionOwnership.ownerMaster.inventory;
                        ownerInventory.onInventoryChanged += MirrorInventory;
                        MirrorInventory();
                    }
                }
            }

            private void MirrorInventory()
            {
                int stacks = self.inventory.GetItemCountPermanent(DLC3Content.Items.DroneUpgradeHidden);
                self.inventory.CopyItemsFrom(ownerInventory, ItemFilter);
                if (stacks > 0) {
                    self.inventory.GiveItemPermanent(DLC3Content.Items.DroneUpgradeHidden, stacks);
                }
            }

            private bool ItemFilter(ItemIndex index) {
                ItemDef item = ItemCatalog.GetItemDef(index);
                if (item.tier == ItemTier.NoTier) return false;

                foreach (ItemTag tag in blacklistedTags) {
                    if (item.ContainsTag(tag)) {
                        return false;
                    }
                }

                return true;
            }
        }

        private static void ReduceNanoBugDamage(ILContext il)
        {
            ILCursor c = new(il);
            c.TryGotoNext(MoveType.After, x => x.MatchLdfld(out _), x => x.MatchLdarg(2), x => x.MatchLdfld(typeof(DamageInfo), nameof(DamageInfo.damage)));
            c.EmitDelegate<Func<float, float>>((damage) => {
                return damage * (NanoBugDamage * 0.01f);
            });
        }

        private static void Replace(SkillDef skill, string match, string replace) {
            CollectiveRewiring.LanguageLoadMap.Add(skill.skillDescriptionToken, (x) => x.Replace(match, replace));
        }

        private static void Replace(string token, string match, string replace) {
            CollectiveRewiring.LanguageLoadMap.Add(token, (x) => x.Replace(match, replace));
        }

        private class RicochetSound : MonoBehaviour {
            public void OnEnable() {
                AkSoundEngine.PostEvent(Events.Play_bandit2_R_alt_kill, base.gameObject);
                AkSoundEngine.PostEvent(Events.Play_item_proc_moveSpeedOnKill, base.gameObject);
                AkSoundEngine.PostEvent(Events.Play_item_proc_ghostOnKill, base.gameObject);
            }
        }

        private class HealNovaAssumePosition : CommandAssumePosition {
            public override void OnEnter()
            {
                TargetHeadroom = 3.5f;
                base.OnEnter();
            }
            public override EntityState InstantiateNextState()
            {
                return new CommandHealNovaPulse();
            }
        }
 
        private static void DroneTechUpdate(On.DroneTechController.orig_FixedUpdate orig, DroneTechController self)
        {
            orig(self);
            self.dronesDirty = true;
        }

        private static void Recharge(On.RoR2.DroneCommandReceiver.orig_FixedUpdate orig, DroneCommandReceiver self)
        {
            orig(self);

            DroneSkillData data = SkillMap.GetOrCreateValue(self.gameObject);
            data.cooldown += Time.fixedDeltaTime;
            data.cooldown = Mathf.Clamp(data.cooldown, 0f, data.finalRecharge);
        }

        private static bool InternalCD(On.RoR2.DroneCommandReceiver.orig_IsReady orig, DroneCommandReceiver self)
        {
            DroneSkillData data = SkillMap.GetOrCreateValue(self.gameObject);
            
            return orig(self) && data.cooldown >= data.finalRecharge;
        }

        private static void UpdateIcons(On.DroneTechSurvivorUIController.orig_FillSecondaryQueue orig, DroneTechSurvivorUIController self, List<DroneInfo> ReadyDrones, List<DroneInfo> UnReadyDrones)
        {
            orig(self, ReadyDrones, UnReadyDrones);
            ChildLocator holder = self.overlayInstanceChildLocator.FindChild("Secondary Queue").GetComponent<ChildLocator>();
            if (!holder)
            {
                return;
            }

            int num = 0;
            for (int i = 0; i < 3; i++)
            {
                ChildLocator slot = holder.FindChild("Drone " + (i + 1)).GetComponent<ChildLocator>();
                if (!slot) continue;
                
                RawImage type = slot.FindChild("Type").GetComponent<RawImage>();
                RawImage icon = slot.FindChild("Icon").GetComponent<RawImage>();

                if (BackdropSprite == null) {
                    BackdropSprite = slot.FindChild("NoDrone").GetComponent<Image>().sprite;
                }

                DroneInfo droneInfo = null;
                if (i < ReadyDrones.Count)
                {
                    droneInfo = ReadyDrones[i];
                }
                else if (num < UnReadyDrones.Count)
                {
                    droneInfo = UnReadyDrones[num];
                    num++;
                }

                if (droneInfo == null) {
                    continue;
                }

                if (UseDroneICD) {
                    Image image = slot.GetComponent<Image>();
                    image.color = droneInfo.type switch {
                        DroneType.Healing => self.HealingColor,
                        DroneType.Combat => self.DamageColor,
                        DroneType.Utility => self.UtilityColor,
                        _ => self.HealingColor
                    };
                    image.color = Color.Lerp(image.color, Color.black, 0.35f);
                    image.type = Image.Type.Filled;
                    image.fillMethod = Image.FillMethod.Horizontal;
                    image.sprite = BackdropSprite;
                    
                    if (droneInfo.characterBody) {
                        DroneSkillData data = SkillMap.GetOrCreateValue(droneInfo.characterBody.gameObject);
                        image.fillAmount = Mathf.Clamp01(data.cooldown / data.finalRecharge);
                    }
                    else {
                        image.fillAmount = 1f;
                    }
                }

                if (BetterQueueUI) {
                    type.rectTransform.localScale = new Vector3(0.8f, 0.8f, 1f);
                    type.rectTransform.localPosition = new Vector3(160, 0f, 0f);
                    icon.rectTransform.localScale = new Vector3(1.3f, 1.3f, 1f);
                    icon.rectTransform.localPosition = new Vector3(-86, 5, 0);
                }
            }
        }

        private static bool IsReady(On.RoR2.Skills.DroneTechDroneSkillDef.orig_IsReady orig, RoR2.Skills.DroneTechDroneSkillDef self, GenericSkill skillSlot)
        {
            DroneSkillData data = SkillMap.GetOrCreateValue(skillSlot.gameObject);
            data.Setup(skillSlot.gameObject);

            if (data.controller && data.controller.CurrentDrone != null && data.controller.CurrentDrone.characterBody) {
                DroneSkillData droneData = SkillMap.GetOrCreateValue(data.controller.CurrentDrone.characterBody.gameObject);

                return orig(self, skillSlot) && droneData.cooldown >= droneData.finalRecharge;
            }

            return orig(self, skillSlot);
        }

        private static void OnDroneCommand(On.RoR2.DroneCommandReceiver.orig_CommandActivate orig, DroneCommandReceiver self)
        {
            orig(self);

            DroneSkillData data = SkillMap.GetOrCreateValue(self.gameObject);
            data.cooldown = 0f;
            if (self.leaderBody) {
                data.finalRecharge = self.leaderBody.skillLocator.secondary.finalRechargeInterval;
            }
        }

        private static FixedConditionalWeakTable<Follow, FollowData> FollowMap = new();
        private static FixedConditionalWeakTable<GameObject, DroneSkillData> SkillMap = new();
        private class DroneSkillData {
            public DroneTechController controller;
            public float cooldown = 25f;
            public float finalRecharge = 1f;

            public void Setup(GameObject obj) {
                if (!controller) {
                    controller = obj.GetComponent<DroneTechController>();
                }
            }
        }
        private class FollowData {
            public float stopwatch;
            public CharacterBody target;
            public CharacterBody drone;
            private bool setup = false;

            public void Setup(CharacterBody body, CharacterBody droneBody) {
                if (!setup) {
                    setup = true;
                    target = body;
                    drone = droneBody;
                    if (target.hasAuthority) {
                        target.onSkillActivatedAuthority += OnSkillActivated;
                    }
                    else if (NetworkServer.active) {
                        target.onSkillActivatedServer += OnSkillActivated;
                    }
                }
            }

            private void OnSkillActivated(GenericSkill skill)
            {
                if (skill.skillDef.skillNameToken == "DRONETECH_SECONDARY_NAME") {
                    stopwatch = 5f;
                    drone.inputBank.aimDirection = target.inputBank.aimDirection;
                }
            }
        }
        
        private static void AllowAllyGrab(On.RoR2.DroneCommandReceiver.orig_Awake orig, DroneCommandReceiver self)
        {
            orig(self);

            if (self.targetType.HasFlag(DroneCommandReceiver.TargetType.Chest)) {
                self.targetType |= DroneCommandReceiver.TargetType.Ally;
            }

            if (self.targetType.HasFlag(DroneCommandReceiver.TargetType.Self)) {
                self.targetDistance = 160f;
            }
        }

        private static TargetInfo AllowAllyTarget(On.RoR2.DroneCommandReceiver.orig_DoTargetInfoSearch orig, DroneCommandReceiver self, Ray aimRay)
        {
            DroneCommandReceiver.TargetType targetType = self.targetType;
            bool overriden = false;
            if (targetType.HasFlag(DroneCommandReceiver.TargetType.Self)) {
                self.targetType = DroneCommandReceiver.TargetType.Ally;
                overriden = true;
            }

            TargetInfo info = orig(self, aimRay);
            self.targetType = targetType;

            if (overriden && info.hurtBox) {
                return info;
            }

            return orig(self, aimRay);
        }

        public class OperatorTracker : HurtboxTracker {
            private bool inputTaken = false;
            private DroneTechController tech;
            public override void Start()
            {
                targetingIndicatorPrefab = Paths.GameObject.DroneTechTrackingIndicator;
                maxSearchAngle = 35f;
                maxSearchDistance = 90f;
                targetType = TargetType.Friendly;
                searchDelay = 0.2f;
                base.Start();
                tech = GetComponent<DroneTechController>();
            }
            public override Transform SearchForTarget()
            {
                if (NoGrab) {
                    return null;
                }

                return base.SearchForTarget();
            }
            public override bool Filter(HurtBox box)
            {
                return box && (box.healthComponent.GetComponent<DroneCommandReceiver>()?.IsReady() ?? false) && box.healthComponent.body.GetOwnerBody() == body;
            }
            public override void FixedUpdate()
            {
                base.FixedUpdate();

                if (inputBank && !inputBank.GetButtonState(DroneCycle).down) {
                    inputTaken = false;
                }

                if (body && body.hasAuthority && inputBank.GetButtonState(DroneCycle).down && !inputTaken) {
                    inputTaken = true;

                    if (targetBody && !NoGrab) {
                        tech.DroneQueue.RemoveAll(x => x.characterBody.bodyIndex == targetBody.bodyIndex);
                        var allCopies = tech.AllDrones.Where(x => x.characterBody.bodyIndex == targetBody.bodyIndex && x.characterBody != targetBody);
                        foreach (DroneInfo info in allCopies) {
                            tech.DroneQueue.Insert(0, info);
                        }
                        tech.DroneQueue.Insert(0, tech.AllDrones.First(x => x.characterBody == targetBody));
                        tech.dronesDirty = true;
                    }
                    else {
                        if (tech.DroneQueue.Count > 0) {
                            DroneInfo bottom = tech.DroneQueue.ElementAt(0);
                            tech.DroneQueue.RemoveAt(0);
                            tech.DroneQueue.Add(bottom);
                            tech.dronesDirty = true;
                        }
                    }
                }
            }
        }
    }
}