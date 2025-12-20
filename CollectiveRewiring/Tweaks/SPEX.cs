using System;

namespace CollectiveRewiring {
    [ConfigSection("Tweaks :: SPEX")]
    public static class SPEX {
        [ConfigField("Prevent Accidental Damage", "Only allows SPEX to take damage from player skills and only from players close to it.", true)]
        private static bool AvoidAccidentalDamage;
        private static LazyIndex SPEXBody = new("SolusVendorBody");
        public static void Initialize() {
            On.RoR2.HealthComponent.TakeDamageProcess += OnTakeDamage;
        }

        private static void OnTakeDamage(On.RoR2.HealthComponent.orig_TakeDamageProcess orig, RoR2.HealthComponent self, RoR2.DamageInfo damageInfo)
        {
            if (AvoidAccidentalDamage && self.body && self.body.bodyIndex == SPEXBody) {
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