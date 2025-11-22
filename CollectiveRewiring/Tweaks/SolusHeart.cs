using System;

namespace CollectiveRewiring {
    [ConfigSection("Tweaks :: Solus Heart")]
    public static class SolusHeart {
        [ConfigField("Prevent Accidental Ending Skip", "Only allows the downed Solus Heart to take damage from player skills and only from players close to it.", true)]
        private static bool AvoidAccidentalSkip;
        private static LazyIndex SolusHeartDowned = new("SolusHeartBody_Offering");
        public static void Initialize() {
            On.RoR2.HealthComponent.TakeDamageProcess += OnTakeDamage;
        }

        private static void OnTakeDamage(On.RoR2.HealthComponent.orig_TakeDamageProcess orig, RoR2.HealthComponent self, RoR2.DamageInfo damageInfo)
        {
            if (AvoidAccidentalSkip && self.body && self.body.bodyIndex == SolusHeartDowned) {
                if (damageInfo.damageType.damageSource == DamageSource.NoneSpecified) {
                    goto skip;
                }

                if (!damageInfo.attacker || !damageInfo.attacker.GetComponent<CharacterBody>()) {
                    goto skip;
                }

                if (damageInfo.attacker.TryGetComponent<CharacterBody>(out var body)) {
                    if (!body.isPlayerControlled || Vector3.Distance(self.transform.position, body.transform.position) >= 30f) {
                        goto skip;
                    }
                }

                skip:
                damageInfo.rejected = true;
            }

            orig(self, damageInfo);
        }
    }
}