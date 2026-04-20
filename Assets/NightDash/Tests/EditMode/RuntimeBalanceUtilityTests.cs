// S1-04: RuntimeBalanceUtility regression tests.
// Purpose: Codify the GDD rule "regression test required when deviation exceeds ±20% of baseline".
// All baselines are defined in BalanceBaselines; no production files are modified.

using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using NightDash.ECS.Systems;   // internal — allowed via [InternalsVisibleTo("NightDash.Tests.EditMode")]
using NightDash.ECS.Components;
using NightDash.Data;

namespace NightDash.Tests.EditMode
{
    // ---------------------------------------------------------------------------
    // GDD balance baselines — source: Docs/GDD/specs/08_combat_math_and_balance.md §1.
    // These constants define the PLAYER baseline (level 1, no passives) per GDD.
    // Individual ClassData assets may deviate; drift > ±20% is flagged by S3-08 tests.
    //
    // Last reconciled with asset surveys: 2026-04-19 (S1-12).
    //   Survey results:
    //     - Classes: warrior HP=125 (+25%), mage Power=13 (+30%),
    //       all classes MoveSpeed 3.5~3.8 (-24% ~ -30% vs GDD 5.0) → see S3-08 backlog.
    //     - Weapons: multi-modal (3 melee / 3 projectile);
    //       projectile weapons speed 9~12 match ProjectileWeaponSpeed=10f.
    // ---------------------------------------------------------------------------
    internal static class BalanceBaselines
    {
        public const float Tolerance = 0.20f; // GDD rule: ±20% of baseline

        // Player baseline (GDD §1)
        public const float PlayerBaseHp        = 100f;
        public const float PlayerBaseDamage    = 10f;  // GDD "Power"
        public const float PlayerBaseMoveSpeed = 5f;

        // Weapon baselines — derived from Assets/NightDash/Data/Weapons/*.asset (S1-12 survey).
        // Multi-modal: melee weapons use ProjectileSpeed = 0; projectile weapons use 10.
        public const float WeaponBaseCooldown        = 1f;  // asset median (range 0.8–1.35)
        public const float WeaponBaseRange           = 5f;  // midpoint; melee ≈ 3, projectile ≈ 8.5
        public const float WeaponBasePowerCoeff      = 1f;  // weapon_demon_orb reference
        public const float MeleeProjectileSpeed      = 0f;  // all 3 melee weapons
        public const float ProjectileWeaponSpeed     = 10f; // demon_orb baseline, matches GDD expectation
    }

    // ---------------------------------------------------------------------------
    // Tests
    // ---------------------------------------------------------------------------
    [TestFixture]
    public class RuntimeBalanceUtilityTests
    {
        // ScriptableObject instances — created once per fixture, destroyed in teardown.
        private WeaponData  _weaponData;
        private ClassData   _classData;

        [OneTimeSetUp]
        public void CreateScriptableObjects()
        {
            _weaponData = ScriptableObject.CreateInstance<WeaponData>();
            _weaponData.baseCooldown        = BalanceBaselines.WeaponBaseCooldown;
            _weaponData.baseRange           = BalanceBaselines.WeaponBaseRange;
            _weaponData.basePowerCoeff      = BalanceBaselines.WeaponBasePowerCoeff;
            _weaponData.baseProjectileSpeed = BalanceBaselines.ProjectileWeaponSpeed;
            _weaponData.levelCurves         = null; // explicit null → fallback path

            _classData = ScriptableObject.CreateInstance<ClassData>();
            _classData.baseHp        = (int)BalanceBaselines.PlayerBaseHp;
            _classData.basePower     = BalanceBaselines.PlayerBaseDamage;
            _classData.baseMoveSpeed = BalanceBaselines.PlayerBaseMoveSpeed;
        }

        [OneTimeTearDown]
        public void DestroyScriptableObjects()
        {
            if (_weaponData != null) Object.DestroyImmediate(_weaponData);
            if (_classData  != null) Object.DestroyImmediate(_classData);
        }

        // -----------------------------------------------------------------------
        // Test 1: ResolveWeaponCurve returns base values when levelCurves is null.
        // Validates the fallback path of the curve lookup; no tolerance required.
        // -----------------------------------------------------------------------
        [Test]
        public void ResolveWeaponCurve_Returns_BaseValues_When_LevelCurves_Null()
        {
            // WeaponData.levelCurves == null → pure fallback branch
            WeaponLevelCurve result = RuntimeBalanceUtility.ResolveWeaponCurve(_weaponData, 3);

            Assert.That(result.level,       Is.EqualTo(3),                            "curve.level must echo the requested level");
            Assert.That(result.cooldown,    Is.EqualTo(_weaponData.baseCooldown),     "fallback cooldown must equal baseCooldown");
            Assert.That(result.range,       Is.EqualTo(_weaponData.baseRange),        "fallback range must equal baseRange");
            Assert.That(result.powerCoeff,  Is.EqualTo(_weaponData.basePowerCoeff),   "fallback powerCoeff must equal basePowerCoeff");
        }

        // -----------------------------------------------------------------------
        // Test 2: ResolveWeaponRuntimeProfile damage stays within ±20% of the
        //         GDD powerCoeff curve at levels 1–5.
        //
        // GDD design intent (fixture): level N → powerCoeff 1.0 + (N-1)*0.1
        //   level 1 → 10 * 1.0 = 10.0
        //   level 2 → 10 * 1.1 = 11.0
        //   level 3 → 10 * 1.2 = 12.0
        //   level 4 → 10 * 1.3 = 13.0
        //   level 5 → 10 * 1.4 = 14.0
        //
        // Test locally overrides _weaponData.levelCurves with the fixture curve,
        // then restores null in finally to preserve Test 1's fallback path state.
        // -----------------------------------------------------------------------
        [TestCase(1, 10f * 1.0f)]
        [TestCase(2, 10f * 1.1f)]
        [TestCase(3, 10f * 1.2f)]
        [TestCase(4, 10f * 1.3f)]
        [TestCase(5, 10f * 1.4f)]
        public void ResolveWeaponRuntimeProfile_Damage_Within_20Pct_Of_Fixture_At_Levels_1_To_5(
            int level, float expectedDamage)
        {
            PlayerRuntimeProfile player = new PlayerRuntimeProfile
            {
                Damage                   = BalanceBaselines.PlayerBaseDamage,
                CooldownMultiplier       = 1f,
                RangeMultiplier          = 1f,
                ProjectileSpeedMultiplier = 1f
            };

            _weaponData.levelCurves = new System.Collections.Generic.List<WeaponLevelCurve>
            {
                new WeaponLevelCurve { level = 1, powerCoeff = 1.0f, cooldown = _weaponData.baseCooldown, range = _weaponData.baseRange },
                new WeaponLevelCurve { level = 2, powerCoeff = 1.1f, cooldown = _weaponData.baseCooldown, range = _weaponData.baseRange },
                new WeaponLevelCurve { level = 3, powerCoeff = 1.2f, cooldown = _weaponData.baseCooldown, range = _weaponData.baseRange },
                new WeaponLevelCurve { level = 4, powerCoeff = 1.3f, cooldown = _weaponData.baseCooldown, range = _weaponData.baseRange },
                new WeaponLevelCurve { level = 5, powerCoeff = 1.4f, cooldown = _weaponData.baseCooldown, range = _weaponData.baseRange },
            };
            try
            {
                WeaponRuntimeProfile result = RuntimeBalanceUtility.ResolveWeaponRuntimeProfile(
                    _weaponData, level, player);

                float tolerance = expectedDamage * BalanceBaselines.Tolerance;
                Assert.That(result.Damage, Is.EqualTo(expectedDamage).Within(tolerance),
                    $"Level {level}: damage {result.Damage} is outside ±20% of fixture {expectedDamage}");
            }
            finally
            {
                _weaponData.levelCurves = null;
            }
        }

        // Boundary case: powerCoeff clamp at 0.1f should not produce negative or zero damage.
        [Test]
        public void ResolveWeaponRuntimeProfile_Damage_Clamp_Prevents_Zero_Or_Negative()
        {
            // Create a throwaway WeaponData with powerCoeff = 0 to trigger math.max(0.1f, ...) clamp.
            WeaponData zeroCoeffWeapon = ScriptableObject.CreateInstance<WeaponData>();
            try
            {
                zeroCoeffWeapon.basePowerCoeff      = 0f;
                zeroCoeffWeapon.baseCooldown        = 1f;
                zeroCoeffWeapon.baseRange           = 5f;
                zeroCoeffWeapon.baseProjectileSpeed = 10f;
                zeroCoeffWeapon.levelCurves         = null;

                PlayerRuntimeProfile player = new PlayerRuntimeProfile
                {
                    Damage                    = 10f,
                    CooldownMultiplier        = 1f,
                    RangeMultiplier           = 1f,
                    ProjectileSpeedMultiplier = 1f
                };

                WeaponRuntimeProfile result = RuntimeBalanceUtility.ResolveWeaponRuntimeProfile(
                    zeroCoeffWeapon, 1, player);

                Assert.That(result.Damage, Is.GreaterThan(0f),
                    "Clamp math.max(0.1f, powerCoeff) must keep damage positive even when powerCoeff == 0");
            }
            finally
            {
                Object.DestroyImmediate(zeroCoeffWeapon);
            }
        }

        // -----------------------------------------------------------------------
        // Test 3: ResolvePlayerRuntimeProfile baseline stats match ClassData ±20%.
        // Uses a real World + entity so DynamicBuffer<OwnedPassiveElement> can be
        // obtained through EntityManager (ownedPassives.Length == 0 → no registry call).
        // -----------------------------------------------------------------------
        [Test]
        public void ResolvePlayerRuntimeProfile_Baseline_Stats_Match_ClassData_Within_20Pct()
        {
            World world = new World("S1-04 Balance Test World");
            try
            {
                EntityManager em     = world.EntityManager;
                Entity entity        = em.CreateEntity();
                em.AddBuffer<OwnedPassiveElement>(entity);
                DynamicBuffer<OwnedPassiveElement> passiveBuffer =
                    em.GetBuffer<OwnedPassiveElement>(entity);

                // registry is not called when passiveBuffer is empty → null is safe
                PlayerRuntimeProfile profile =
                    RuntimeBalanceUtility.ResolvePlayerRuntimeProfile(null, _classData, passiveBuffer);

                float hpTol    = BalanceBaselines.PlayerBaseHp        * BalanceBaselines.Tolerance;
                float dmgTol   = BalanceBaselines.PlayerBaseDamage    * BalanceBaselines.Tolerance;
                float speedTol = BalanceBaselines.PlayerBaseMoveSpeed * BalanceBaselines.Tolerance;

                Assert.That(profile.MaxHealth,  Is.EqualTo(BalanceBaselines.PlayerBaseHp).Within(hpTol),
                    "MaxHealth must be within ±20% of GDD baseline");
                Assert.That(profile.Damage,     Is.EqualTo(BalanceBaselines.PlayerBaseDamage).Within(dmgTol),
                    "Damage must be within ±20% of GDD baseline");
                Assert.That(profile.MoveSpeed,  Is.EqualTo(BalanceBaselines.PlayerBaseMoveSpeed).Within(speedTol),
                    "MoveSpeed must be within ±20% of GDD baseline");
            }
            finally
            {
                world.Dispose();
            }
        }

        // -----------------------------------------------------------------------
        // Test 4: Default multipliers (Cooldown / Range / ProjectileSpeed) must be
        //         1f with no passives applied — tolerance ±20% (0.8 ~ 1.2).
        // -----------------------------------------------------------------------
        [Test]
        public void ResolvePlayerRuntimeProfile_Cooldown_Range_Projectile_Multipliers_Default_To_1()
        {
            World world = new World("S1-04 Multiplier Test World");
            try
            {
                EntityManager em     = world.EntityManager;
                Entity entity        = em.CreateEntity();
                em.AddBuffer<OwnedPassiveElement>(entity);
                DynamicBuffer<OwnedPassiveElement> passiveBuffer =
                    em.GetBuffer<OwnedPassiveElement>(entity);

                PlayerRuntimeProfile profile =
                    RuntimeBalanceUtility.ResolvePlayerRuntimeProfile(null, _classData, passiveBuffer);

                const float expectedMultiplier = 1f;
                float       tol               = expectedMultiplier * BalanceBaselines.Tolerance; // 0.2

                Assert.That(profile.CooldownMultiplier, Is.EqualTo(expectedMultiplier).Within(tol),
                    "CooldownMultiplier must default to 1f (no passives applied)");
                Assert.That(profile.RangeMultiplier, Is.EqualTo(expectedMultiplier).Within(tol),
                    "RangeMultiplier must default to 1f (no passives applied)");
                Assert.That(profile.ProjectileSpeedMultiplier, Is.EqualTo(expectedMultiplier).Within(tol),
                    "ProjectileSpeedMultiplier must default to 1f (no passives applied)");
            }
            finally
            {
                world.Dispose();
            }
        }
    }

    // ---------------------------------------------------------------------------
    // S1-11: StatAccumulator math verification (GDD balance formula).
    //   Final = (Base + Sum(Flat)) * (1 + Sum(PercentAdd)) * Product(1 + PercentMul)
    // These tests validate the fix that separated PercentAdd (sum) from
    // PercentMul (product) — previously both were collapsed into product style.
    // ---------------------------------------------------------------------------
    [TestFixture]
    public class StatAccumulatorTests
    {
        private const float Epsilon = 0.0001f;

        [Test]
        public void Default_Finalize_Returns_Base_Untouched()
        {
            var acc = RuntimeBalanceUtility.StatAccumulator.Default;
            Assert.That(acc.Finalize(100f), Is.EqualTo(100f).Within(Epsilon));
        }

        [Test]
        public void Flat_Adds_To_Base()
        {
            var acc = RuntimeBalanceUtility.StatAccumulator.Default;
            acc.Apply(StatOperation.Flat, 25f);
            Assert.That(acc.Finalize(100f), Is.EqualTo(125f).Within(Epsilon));
        }

        // Core S1-11 regression: two PercentAdd effects must SUM before multiplying,
        // not compound as product. Previous buggy code: 100 * 1.1 * 1.15 = 126.5
        // GDD-correct result:  (100) * (1 + 0.10 + 0.15) = 125.0
        [Test]
        public void Multiple_PercentAdd_Sum_Before_Single_Multiply()
        {
            var acc = RuntimeBalanceUtility.StatAccumulator.Default;
            acc.Apply(StatOperation.PercentAdd, 0.10f);
            acc.Apply(StatOperation.PercentAdd, 0.15f);
            Assert.That(acc.Finalize(100f), Is.EqualTo(125f).Within(Epsilon),
                "PercentAdd effects must sum first, then apply (1 + sum) once");
        }

        // PercentMul effects compound as product (Π (1 + v))
        [Test]
        public void Multiple_PercentMul_Compound_As_Product()
        {
            var acc = RuntimeBalanceUtility.StatAccumulator.Default;
            acc.Apply(StatOperation.PercentMul, 0.10f);
            acc.Apply(StatOperation.PercentMul, 0.15f);
            // 100 * 1.1 * 1.15 = 126.5
            Assert.That(acc.Finalize(100f), Is.EqualTo(126.5f).Within(Epsilon),
                "PercentMul effects must compound as Product(1 + value)");
        }

        // Full GDD formula: Final = (Base + Sum(Flat)) * (1 + Sum(PercentAdd)) * Product(1 + PercentMul)
        // Base = 100, Flat = 10, PercentAdd sum = 0.2, PercentMul = 0.1
        // Expected = (100 + 10) * (1 + 0.2) * (1 + 0.1) = 110 * 1.2 * 1.1 = 145.2
        [Test]
        public void Full_GDD_Formula_Mixed_Operations()
        {
            var acc = RuntimeBalanceUtility.StatAccumulator.Default;
            acc.Apply(StatOperation.Flat, 10f);
            acc.Apply(StatOperation.PercentAdd, 0.10f);
            acc.Apply(StatOperation.PercentAdd, 0.10f);
            acc.Apply(StatOperation.PercentMul, 0.10f);
            Assert.That(acc.Finalize(100f), Is.EqualTo(145.2f).Within(Epsilon),
                "Full formula: (Base + Flat) * (1 + ΣPercentAdd) * Π(1 + PercentMul)");
        }

        // PercentAdd and PercentMul of equal single magnitude yield the SAME result
        // (both collapse to base * (1 + v)). This is the case that masked the S1-11 bug
        // historically — it only surfaces when multiple PercentAdd effects stack.
        [Test]
        public void Single_PercentAdd_And_PercentMul_Produce_Identical_Result()
        {
            var addAcc = RuntimeBalanceUtility.StatAccumulator.Default;
            addAcc.Apply(StatOperation.PercentAdd, 0.10f);

            var mulAcc = RuntimeBalanceUtility.StatAccumulator.Default;
            mulAcc.Apply(StatOperation.PercentMul, 0.10f);

            Assert.That(addAcc.Finalize(100f), Is.EqualTo(mulAcc.Finalize(100f)).Within(Epsilon),
                "Single-effect PercentAdd and PercentMul must match (historical parity)");
        }
    }
}
