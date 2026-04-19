using NightDash.Data;
using NightDash.ECS.Components;
using NightDash.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace NightDash.ECS.Systems
{
    internal struct PlayerRuntimeProfile
    {
        public float MaxHealth;
        public float Damage;
        public float MoveSpeed;
        public float CooldownMultiplier;
        public float RangeMultiplier;
        public float ProjectileSpeedMultiplier;
    }

    internal struct WeaponRuntimeProfile
    {
        public float Damage;
        public float Cooldown;
        public float Range;
        public float ProjectileSpeed;
    }

    internal static class RuntimeBalanceUtility
    {
        public static void ResetRunBuffers(ref SystemState state)
        {
            DynamicBuffer<OwnedWeaponElement> ownedWeapons = GetSingletonBuffer<OwnedWeaponElement>(ref state);
            DynamicBuffer<OwnedPassiveElement> ownedPassives = GetSingletonBuffer<OwnedPassiveElement>(ref state);
            DynamicBuffer<UpgradeOptionElement> upgradeOptions = GetSingletonBuffer<UpgradeOptionElement>(ref state);
            DynamicBuffer<AvailableWeaponElement> availableWeapons = GetSingletonBuffer<AvailableWeaponElement>(ref state);
            DynamicBuffer<AvailablePassiveElement> availablePassives = GetSingletonBuffer<AvailablePassiveElement>(ref state);

            ownedWeapons.Clear();
            ownedPassives.Clear();
            upgradeOptions.Clear();
            availableWeapons.Clear();
            availablePassives.Clear();
        }

        public static void PopulateAvailableUpgrades(ref SystemState state, DataRegistry registry)
        {
            DynamicBuffer<AvailableWeaponElement> availableWeapons = GetSingletonBuffer<AvailableWeaponElement>(ref state);
            DynamicBuffer<AvailablePassiveElement> availablePassives = GetSingletonBuffer<AvailablePassiveElement>(ref state);

            if (registry?.Catalog?.weapons != null)
            {
                for (int i = 0; i < registry.Catalog.weapons.Count; i++)
                {
                    WeaponData weapon = registry.Catalog.weapons[i];
                    if (weapon == null || string.IsNullOrWhiteSpace(weapon.id))
                    {
                        continue;
                    }

                    if (!weapon.includeInUpgradePool)
                    {
                        continue;
                    }

                    availableWeapons.Add(new AvailableWeaponElement { Id = weapon.id.Trim() });
                }
            }

            if (registry?.Catalog?.passives != null)
            {
                for (int i = 0; i < registry.Catalog.passives.Count; i++)
                {
                    PassiveData passive = registry.Catalog.passives[i];
                    if (passive == null || string.IsNullOrWhiteSpace(passive.id))
                    {
                        continue;
                    }

                    availablePassives.Add(new AvailablePassiveElement { Id = passive.id.Trim() });
                }
            }
        }

        public static void AddStartingLoadout(ref SystemState state, DataRegistry registry, string classId)
        {
            if (registry == null || !registry.TryGetClass(classId, out ClassData classData) || classData == null)
            {
                return;
            }

            DynamicBuffer<OwnedWeaponElement> ownedWeapons = GetSingletonBuffer<OwnedWeaponElement>(ref state);
            DynamicBuffer<OwnedPassiveElement> ownedPassives = GetSingletonBuffer<OwnedPassiveElement>(ref state);

            if (!string.IsNullOrWhiteSpace(classData.startWeaponId) && registry.TryGetWeapon(classData.startWeaponId, out WeaponData startWeapon))
            {
                ownedWeapons.Add(new OwnedWeaponElement
                {
                    Id = startWeapon.id.Trim(),
                    Level = 1,
                    MaxLevel = math.max(1, startWeapon.maxLevel),
                    CooldownRemaining = 0f
                });
            }

            if (!string.IsNullOrWhiteSpace(classData.uniquePassiveId) && registry.TryGetPassive(classData.uniquePassiveId, out PassiveData startPassive))
            {
                ownedPassives.Add(new OwnedPassiveElement
                {
                    Id = startPassive.id.Trim(),
                    Level = 1,
                    MaxLevel = math.max(1, startPassive.maxLevel)
                });
            }
        }

        public static void RefreshPlayerRuntime(ref SystemState state, DataRegistry registry, string classId)
        {
            if (registry == null || !registry.TryGetClass(classId, out ClassData classData) || classData == null)
            {
                return;
            }

            DynamicBuffer<OwnedWeaponElement> ownedWeapons = GetSingletonBuffer<OwnedWeaponElement>(ref state);
            DynamicBuffer<OwnedPassiveElement> ownedPassives = GetSingletonBuffer<OwnedPassiveElement>(ref state);

            PlayerRuntimeProfile profile = ResolvePlayerRuntimeProfile(registry, classData, ownedPassives);

            string primaryWeaponId = classData.startWeaponId;
            int primaryWeaponLevel = 1;
            if (ownedWeapons.Length > 0)
            {
                primaryWeaponId = ownedWeapons[0].Id.ToString();
                primaryWeaponLevel = math.max(1, ownedWeapons[0].Level);
            }

            float weaponCooldown = 0.9f;
            float weaponRange = 5f;
            float projectileSpeed = 10f;
            float weaponDamage = profile.Damage;
            if (!string.IsNullOrWhiteSpace(primaryWeaponId) && registry.TryGetWeapon(primaryWeaponId, out WeaponData weaponData) && weaponData != null)
            {
                WeaponRuntimeProfile weaponProfile = ResolveWeaponRuntimeProfile(weaponData, primaryWeaponLevel, profile);
                weaponDamage = weaponProfile.Damage;
                weaponCooldown = weaponProfile.Cooldown;
                weaponRange = weaponProfile.Range;
                projectileSpeed = weaponProfile.ProjectileSpeed;
            }

            EntityQuery playerQuery = state.EntityManager.CreateEntityQuery(
                ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.ReadWrite<CombatStats>(),
                ComponentType.ReadWrite<WeaponRuntimeData>(),
                ComponentType.ReadOnly<PlayerTag>());

            using NativeArray<Entity> playerEntities = playerQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < playerEntities.Length; i++)
            {
                Entity player = playerEntities[i];
                LocalTransform transform = state.EntityManager.GetComponentData<LocalTransform>(player);
                CombatStats stats = state.EntityManager.GetComponentData<CombatStats>(player);
                WeaponRuntimeData weapon = state.EntityManager.GetComponentData<WeaponRuntimeData>(player);
                float hpRatio = stats.MaxHealth > 0f ? stats.CurrentHealth / stats.MaxHealth : 1f;
                float3 position = transform.Position;

                stats.MaxHealth = profile.MaxHealth;
                stats.CurrentHealth = math.clamp(profile.MaxHealth * math.saturate(hpRatio), 1f, profile.MaxHealth);
                stats.Damage = profile.Damage;
                stats.MoveSpeed = profile.MoveSpeed;

                weapon.Damage = weaponDamage;
                weapon.Cooldown = weaponCooldown;
                weapon.Range = weaponRange;
                weapon.ProjectileSpeed = projectileSpeed;
                weapon.CooldownRemaining = math.min(weapon.CooldownRemaining, weaponCooldown);

                transform.Position = new float3(position.x, position.y, 0f);

                state.EntityManager.SetComponentData(player, transform);
                state.EntityManager.SetComponentData(player, stats);
                state.EntityManager.SetComponentData(player, weapon);
            }
        }

        public static WeaponLevelCurve ResolveWeaponCurve(WeaponData data, int level)
        {
            WeaponLevelCurve fallback = new WeaponLevelCurve
            {
                level = level,
                powerCoeff = data.basePowerCoeff,
                cooldown = data.baseCooldown,
                range = data.baseRange
            };

            if (data.levelCurves == null)
            {
                return fallback;
            }

            for (int i = 0; i < data.levelCurves.Count; i++)
            {
                if (data.levelCurves[i].level == level)
                {
                    return data.levelCurves[i];
                }
            }

            return fallback;
        }

        public static PlayerRuntimeProfile ResolvePlayerRuntimeProfile(
            DataRegistry registry,
            ClassData classData,
            DynamicBuffer<OwnedPassiveElement> ownedPassives)
        {
            // Baseline values; legacy per-passive multipliers compound directly onto these.
            float baseHp            = classData.baseHp;
            float baseDamage        = classData.basePower;
            float baseMoveSpeed     = classData.baseMoveSpeed;
            float baseCooldownMul   = 1f;
            float baseRangeMul      = 1f;
            float baseProjSpeedMul  = 1f;

            // Per-stat accumulators for passive effects (GDD formula):
            //   Final = (Base + Sum(Flat)) * (1 + Sum(PercentAdd)) * Product(1 + PercentMul)
            StatAccumulator hpAcc       = StatAccumulator.Default;
            StatAccumulator dmgAcc      = StatAccumulator.Default;
            StatAccumulator spdAcc      = StatAccumulator.Default;
            StatAccumulator cdAcc       = StatAccumulator.Default;
            StatAccumulator rngAcc      = StatAccumulator.Default;
            StatAccumulator projAcc     = StatAccumulator.Default;

            for (int i = 0; i < ownedPassives.Length; i++)
            {
                OwnedPassiveElement owned = ownedPassives[i];
                if (registry == null || !registry.TryGetPassive(owned.Id.ToString(), out PassiveData passive) || passive == null)
                {
                    continue;
                }

                float levelScale = math.max(1, owned.Level);
                for (int effectIndex = 0; effectIndex < passive.effects.Count; effectIndex++)
                {
                    PassiveEffect effect = passive.effects[effectIndex];
                    float value = effect.value * levelScale;
                    switch ((effect.stat ?? string.Empty).Trim().ToLowerInvariant())
                    {
                        case "hp":
                        case "health":
                        case "max_hp":
                            hpAcc.Apply(effect.op, value);
                            break;
                        case "power":
                        case "damage":
                            dmgAcc.Apply(effect.op, value);
                            break;
                        case "move_speed":
                        case "speed":
                            spdAcc.Apply(effect.op, value);
                            break;
                        case "cooldown":
                            cdAcc.Apply(effect.op, value);
                            break;
                        case "range":
                            rngAcc.Apply(effect.op, value);
                            break;
                        case "projectile_speed":
                            projAcc.Apply(effect.op, value);
                            break;
                    }
                }

                // Legacy multipliers still compound per-passive directly on the base value.
                baseDamage       *= math.max(0.1f, passive.legacyDamageMultiplier);
                baseHp           *= math.max(0.1f, passive.legacyHealthMultiplier);
                baseCooldownMul  *= math.max(0.1f, passive.legacyCooldownMultiplier);
            }

            return new PlayerRuntimeProfile
            {
                MaxHealth                 = hpAcc.Finalize(baseHp),
                Damage                    = dmgAcc.Finalize(baseDamage),
                MoveSpeed                 = spdAcc.Finalize(baseMoveSpeed),
                CooldownMultiplier        = cdAcc.Finalize(baseCooldownMul),
                RangeMultiplier           = rngAcc.Finalize(baseRangeMul),
                ProjectileSpeedMultiplier = projAcc.Finalize(baseProjSpeedMul)
            };
        }

        public static WeaponRuntimeProfile ResolveWeaponRuntimeProfile(
            WeaponData weaponData,
            int level,
            PlayerRuntimeProfile playerProfile)
        {
            WeaponLevelCurve curve = ResolveWeaponCurve(weaponData, level);
            float baseCooldown = curve.cooldown > 0f ? curve.cooldown : weaponData.baseCooldown;
            float baseRange = curve.range > 0f ? curve.range : weaponData.baseRange;
            float powerCoeff = curve.powerCoeff > 0f ? curve.powerCoeff : weaponData.basePowerCoeff;

            return new WeaponRuntimeProfile
            {
                Damage = playerProfile.Damage * math.max(0.1f, powerCoeff),
                Cooldown = math.max(0.08f, baseCooldown * math.max(0.15f, playerProfile.CooldownMultiplier)),
                Range = math.max(1.5f, baseRange * math.max(0.1f, playerProfile.RangeMultiplier)),
                ProjectileSpeed = math.max(2f, weaponData.baseProjectileSpeed * math.max(0.1f, playerProfile.ProjectileSpeedMultiplier))
            };
        }

        // Per-stat accumulator implementing the GDD balance formula:
        //   Final = (Base + Sum(Flat)) * (1 + Sum(PercentAdd)) * Product(1 + PercentMul)
        // Replaces the previous ApplyEffect which collapsed PercentAdd and PercentMul
        // into the same product-style formula (S1-11 fix).
        internal struct StatAccumulator
        {
            public float Flat;
            public float PercentAddSum;
            public float PercentMulProduct;

            public static StatAccumulator Default =>
                new StatAccumulator { Flat = 0f, PercentAddSum = 0f, PercentMulProduct = 1f };

            public void Apply(StatOperation op, float amount)
            {
                switch (op)
                {
                    case StatOperation.Flat:
                        Flat += amount;
                        break;
                    case StatOperation.PercentAdd:
                        PercentAddSum += amount;
                        break;
                    case StatOperation.PercentMul:
                        PercentMulProduct *= 1f + amount;
                        break;
                }
            }

            public float Finalize(float baseValue)
            {
                return (baseValue + Flat) * (1f + PercentAddSum) * PercentMulProduct;
            }
        }

        private static DynamicBuffer<T> GetSingletonBuffer<T>(ref SystemState state)
            where T : unmanaged, IBufferElementData
        {
            Entity singletonEntity = state.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<T>()).GetSingletonEntity();
            return state.EntityManager.GetBuffer<T>(singletonEntity);
        }
    }
}
