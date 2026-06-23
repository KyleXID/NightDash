using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using NightDash.Data;
using NightDash.Runtime;
using NightDash.ECS.Components;

namespace NightDash.ECS.Systems
{
    /// <summary>
    /// Shared evolution logic used by the level-up card pipeline
    /// (UpgradeOptionUtility / UpgradeApplySystem) and the F8 debug force path
    /// (EvolutionSystem). Evolution is delivered as a level-up CARD: once a weapon
    /// is at max level AND every required passive is maxed, it is flagged
    /// GUARANTEED for the next card set; if skipped it then appears by chance.
    /// </summary>
    internal static class EvolutionUtility
    {
        // Chance a skipped (no-longer-guaranteed) evolution re-appears on a later
        // level-up. Tunable.
        internal const float ReofferChance = 0.5f;
        // Level-up card sets hold at most three cards.
        private const int MaxCards = 3;

        // ----------------------------------------------------------------- eligibility

        internal static bool IsEvolvedId(string weaponId)
            => weaponId.EndsWith("_evolved") || weaponId.EndsWith("_abyss");

        internal static string StripVariantSuffix(string weaponId)
        {
            if (weaponId.EndsWith("_evolved")) return weaponId.Substring(0, weaponId.Length - "_evolved".Length);
            if (weaponId.EndsWith("_abyss")) return weaponId.Substring(0, weaponId.Length - "_abyss".Length);
            return weaponId;
        }

        // Passive ids currently at (or above) their max level.
        internal static HashSet<string> ComputeMaxedPassiveIds(
            DataRegistry registry, DynamicBuffer<OwnedPassiveElement> ownedPassives)
        {
            var set = new HashSet<string>();
            for (int i = 0; i < ownedPassives.Length; i++)
            {
                OwnedPassiveElement p = ownedPassives[i];
                string pid = p.Id.ToString();
                if (registry.TryGetPassive(pid, out PassiveData pd) && pd != null && p.Level >= pd.maxLevel)
                {
                    set.Add(pid);
                }
            }
            return set;
        }

        internal static bool IsWeaponMaxed(DataRegistry registry, OwnedWeaponElement weapon)
            => registry.TryGetWeapon(weapon.Id.ToString(), out WeaponData wd) && wd != null
               && weapon.Level >= wd.maxLevel;

        // ----------------------------------------------------------------- pure resolver

        /// <summary>
        /// Pure evolution-selection logic (no ECS deps → unit-testable). Returns
        /// the first-tier ("_evolved") evolution a weapon qualifies for, or null.
        /// Abyss forms are skipped (the card targets the first tier, which the
        /// evolution VFX is authored for). Highest <c>priority</c> wins on ties.
        /// When <paramref name="requireConditions"/> is true the weapon must be
        /// maxed AND every required passive must be in <paramref name="maxedPassiveIds"/>.
        /// </summary>
        internal static EvolutionData ResolveEvolutionFor(
            List<EvolutionData> evolutions,
            string weaponId,
            bool weaponMaxed,
            HashSet<string> maxedPassiveIds,
            bool requireConditions)
        {
            if (evolutions == null || string.IsNullOrEmpty(weaponId))
            {
                return null;
            }
            if (requireConditions && !weaponMaxed)
            {
                return null;
            }

            EvolutionData best = null;
            for (int i = 0; i < evolutions.Count; i++)
            {
                EvolutionData e = evolutions[i];
                if (e == null || e.requiredWeaponId != weaponId)
                {
                    continue;
                }
                if (e.isAbyss)
                {
                    continue; // first-tier only
                }
                if (requireConditions && !HasAllMaxedPassives(e, maxedPassiveIds))
                {
                    continue;
                }
                if (best == null || e.priority > best.priority)
                {
                    best = e;
                }
            }
            return best;
        }

        private static bool HasAllMaxedPassives(EvolutionData e, HashSet<string> maxedPassiveIds)
        {
            if (e.requiredPassiveIds == null)
            {
                return true;
            }
            for (int i = 0; i < e.requiredPassiveIds.Count; i++)
            {
                string passiveId = e.requiredPassiveIds[i];
                if (string.IsNullOrWhiteSpace(passiveId))
                {
                    continue;
                }
                if (maxedPassiveIds == null || !maxedPassiveIds.Contains(passiveId))
                {
                    return false;
                }
            }
            return true;
        }

        // ----------------------------------------------------------------- offer flags

        /// <summary>
        /// Flags newly-eligible weapons GUARANTEED (EvolutionOffer = 1). Call after
        /// a level-up applies (the only time weapon/passive levels change). Returns
        /// true if any flag changed.
        /// </summary>
        internal static bool RefreshEvolutionOffers(
            DataRegistry registry,
            ref DynamicBuffer<OwnedWeaponElement> ownedWeapons,
            DynamicBuffer<OwnedPassiveElement> ownedPassives)
        {
            if (registry?.Catalog?.evolutions == null)
            {
                return false;
            }

            HashSet<string> maxedPassiveIds = ComputeMaxedPassiveIds(registry, ownedPassives);
            bool changed = false;
            for (int i = 0; i < ownedWeapons.Length; i++)
            {
                OwnedWeaponElement weapon = ownedWeapons[i];
                if (weapon.EvolutionOffer != 0)
                {
                    // Already eligible (1 or 2) — no need to re-evaluate. The card
                    // actually offered always reflects the current best evolution
                    // because TryBuildEvolutionCard re-resolves it (priority-aware)
                    // at injection time; the flag only tracks guaranteed-vs-chance.
                    continue;
                }
                string id = weapon.Id.ToString();
                if (IsEvolvedId(id))
                {
                    continue; // already an evolved form
                }
                if (!IsWeaponMaxed(registry, weapon))
                {
                    continue;
                }
                EvolutionData evo = ResolveEvolutionFor(
                    registry.Catalog.evolutions, id, weaponMaxed: true, maxedPassiveIds, requireConditions: true);
                if (evo != null)
                {
                    weapon.EvolutionOffer = 1; // guaranteed next level-up
                    ownedWeapons[i] = weapon;
                    changed = true;
                }
            }
            return changed;
        }

        /// <summary>
        /// Demotes GUARANTEED (1) offers to PROBABILISTIC (2) — but ONLY for
        /// weapons whose evolution card was actually present in the level-up set
        /// that just resolved (<paramref name="shownOptions"/>). A guaranteed card
        /// that got crowded out of the 3 slots keeps its guarantee for next time.
        /// This also means picking weapon A's evolution does not silently demote a
        /// guaranteed weapon B that was never shown.
        /// </summary>
        internal static void DemoteShownGuaranteedOffers(
            DataRegistry registry,
            ref DynamicBuffer<OwnedWeaponElement> ownedWeapons,
            DynamicBuffer<UpgradeOptionElement> shownOptions)
        {
            if (registry?.Catalog?.evolutions == null)
            {
                return;
            }
            for (int i = 0; i < ownedWeapons.Length; i++)
            {
                OwnedWeaponElement weapon = ownedWeapons[i];
                if (weapon.EvolutionOffer != 1)
                {
                    continue;
                }
                EvolutionData evo = ResolveEvolutionFor(
                    registry.Catalog.evolutions, weapon.Id.ToString(),
                    weaponMaxed: false, maxedPassiveIds: null, requireConditions: false);
                if (evo == null || string.IsNullOrWhiteSpace(evo.resultWeaponId))
                {
                    continue;
                }
                if (ContainsEvolution(shownOptions, new FixedString64Bytes(evo.resultWeaponId)))
                {
                    weapon.EvolutionOffer = 2; // was offered this level-up, not taken → now a chance card
                    ownedWeapons[i] = weapon;
                }
            }
        }

        // ----------------------------------------------------------------- card build / inject

        internal static bool TryBuildEvolutionCard(
            DataRegistry registry, OwnedWeaponElement weapon, out UpgradeOptionElement card)
        {
            card = default;
            EvolutionData evo = ResolveEvolutionFor(
                registry.Catalog?.evolutions, weapon.Id.ToString(),
                weaponMaxed: false, maxedPassiveIds: null, requireConditions: false);
            if (evo == null || string.IsNullOrWhiteSpace(evo.resultWeaponId))
            {
                return false;
            }

            int maxLevel = math.max(1, weapon.MaxLevel);
            if (registry.TryGetWeapon(evo.resultWeaponId, out WeaponData wd) && wd != null)
            {
                maxLevel = math.max(1, wd.maxLevel);
            }

            card = new UpgradeOptionElement
            {
                Kind = UpgradeKind.Evolution,
                Id = new FixedString64Bytes(evo.resultWeaponId),
                CurrentLevel = weapon.Level,
                NextLevel = weapon.Level,
                MaxLevel = maxLevel,
                Rarity = 2 // evolution is always shown on the legendary frame
            };
            return true;
        }

        /// <summary>
        /// Injects evolution cards into a freshly-built option set. GUARANTEED (1)
        /// offers are always inserted; PROBABILISTIC (2) offers are inserted with
        /// <see cref="ReofferChance"/>. Inserting replaces a normal card slot when
        /// the set is full so a guaranteed evolution is never crowded out.
        /// </summary>
        internal static void InjectEvolutionOptions(
            DataRegistry registry,
            ref DynamicBuffer<UpgradeOptionElement> options,
            DynamicBuffer<OwnedWeaponElement> ownedWeapons,
            ref Unity.Mathematics.Random rng)
        {
            if (registry?.Catalog?.evolutions == null)
            {
                return;
            }

            for (int i = 0; i < ownedWeapons.Length; i++)
            {
                OwnedWeaponElement weapon = ownedWeapons[i];
                if (weapon.EvolutionOffer == 0)
                {
                    continue;
                }
                if (weapon.EvolutionOffer == 2 && rng.NextFloat() >= ReofferChance)
                {
                    continue; // chance card not drawn this time
                }
                if (!TryBuildEvolutionCard(registry, weapon, out UpgradeOptionElement card))
                {
                    continue;
                }
                if (ContainsEvolution(options, card.Id))
                {
                    continue; // already in the set
                }
                InsertOrReplace(ref options, card);
            }
        }

        private static bool ContainsEvolution(DynamicBuffer<UpgradeOptionElement> options, FixedString64Bytes id)
        {
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i].Kind == UpgradeKind.Evolution && options[i].Id.Equals(id))
                {
                    return true;
                }
            }
            return false;
        }

        private static void InsertOrReplace(ref DynamicBuffer<UpgradeOptionElement> options, UpgradeOptionElement card)
        {
            if (options.Length < MaxCards)
            {
                options.Add(card);
                return;
            }
            // Full: overwrite a normal (non-evolution) slot, preferring the last,
            // so a guaranteed evolution is never crowded out by a regular card.
            for (int i = options.Length - 1; i >= 0; i--)
            {
                if (options[i].Kind != UpgradeKind.Evolution)
                {
                    options[i] = card;
                    return;
                }
            }
            // All slots are already evolutions — nothing to replace.
        }

        // ----------------------------------------------------------------- apply / swap

        /// <summary>
        /// Applies a chosen evolution card: swaps the matching owned (base) weapon
        /// to the card's result id, keeping the level (clamped) and re-arming the
        /// cooldown so the evolved form fires next tick. Returns true on swap.
        /// </summary>
        internal static bool ApplyEvolutionCard(
            DataRegistry registry, ref DynamicBuffer<OwnedWeaponElement> ownedWeapons, FixedString64Bytes resultId)
        {
            string resultStr = resultId.ToString();
            string baseId = StripVariantSuffix(resultStr);
            for (int i = 0; i < ownedWeapons.Length; i++)
            {
                if (ownedWeapons[i].Id.ToString() != baseId)
                {
                    continue;
                }
                ownedWeapons[i] = SwapToResult(registry, ownedWeapons[i], resultStr);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Debug (F8): evolves every owned weapon that has any first-tier evolution,
        /// ignoring all conditions. Returns true if anything changed.
        /// </summary>
        internal static bool ForceEvolveAll(
            DataRegistry registry, ref DynamicBuffer<OwnedWeaponElement> ownedWeapons)
        {
            if (registry?.Catalog?.evolutions == null)
            {
                return false;
            }
            bool changed = false;
            for (int i = 0; i < ownedWeapons.Length; i++)
            {
                OwnedWeaponElement weapon = ownedWeapons[i];
                EvolutionData evo = ResolveEvolutionFor(
                    registry.Catalog.evolutions, weapon.Id.ToString(),
                    weaponMaxed: false, maxedPassiveIds: null, requireConditions: false);
                if (evo == null || string.IsNullOrWhiteSpace(evo.resultWeaponId))
                {
                    continue;
                }
                ownedWeapons[i] = SwapToResult(registry, weapon, evo.resultWeaponId);
                changed = true;
            }
            return changed;
        }

        private static OwnedWeaponElement SwapToResult(DataRegistry registry, OwnedWeaponElement weapon, string resultId)
        {
            weapon.Id = new FixedString64Bytes(resultId);
            if (registry.TryGetWeapon(resultId, out WeaponData wd) && wd != null)
            {
                weapon.MaxLevel = wd.maxLevel;
                if (weapon.Level > wd.maxLevel)
                {
                    weapon.Level = wd.maxLevel;
                }
            }
            weapon.EvolutionOffer = 0;
            weapon.CooldownRemaining = 0f; // fire the evolved form on the next tick
            return weapon;
        }

        // ----------------------------------------------------------------- orbit cleanup

        /// <summary>
        /// Persistent orbit weapons (ring / blades / barrier) spawn ONCE with a
        /// ~infinite lifetime and never re-fire. After an evolution swap their old
        /// entities still carry the pre-evolution WeaponId, so without cleanup the
        /// stale base orbit keeps running ALONGSIDE the evolved one (double damage +
        /// double visuals). Destroys any persistent orbit whose WeaponId is no
        /// longer owned; WeaponSystem re-spawns the evolved form next tick.
        /// </summary>
        internal static void CleanupOrphanOrbits(
            ref SystemState state,
            DynamicBuffer<OwnedWeaponElement> ownedWeapons,
            EntityCommandBuffer ecb)
        {
            EntityQuery query = state.GetEntityQuery(ComponentType.ReadOnly<ProjectileData>());
            using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
            using NativeArray<ProjectileData> projectiles = query.ToComponentDataArray<ProjectileData>(Allocator.Temp);
            for (int i = 0; i < projectiles.Length; i++)
            {
                ProjectileData p = projectiles[i];
                if (p.Behavior != (byte)ProjectileBehavior.Orbit) continue;
                if (p.Lifetime < 100f) continue; // persistent (≈99999) orbits only, not transient sweeps

                bool stillOwned = false;
                for (int w = 0; w < ownedWeapons.Length; w++)
                {
                    if (ownedWeapons[w].Id == p.WeaponId) { stillOwned = true; break; }
                }
                if (!stillOwned)
                {
                    ecb.DestroyEntity(entities[i]);
                }
            }
        }
    }
}
