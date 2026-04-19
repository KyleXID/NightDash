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
    // GDD balance baseline constants (S1-04 warrior / single-weapon fixture).
    // Update these values only when the GDD baseline table is officially revised.
    // ---------------------------------------------------------------------------
    internal static class BalanceBaselines
    {
        public const float Tolerance = 0.20f; // GDD rule: ±20% of baseline

        // Warrior baseline fixture
        public const float WarriorBaseHp        = 100f;
        public const float WarriorBaseDamage    = 10f;
        public const float WarriorBaseMoveSpeed = 4f;

        // Single-weapon level-1 fixture
        public const float WeaponBaseCooldown        = 1f;
        public const float WeaponBaseRange           = 5f;
        public const float WeaponBasePowerCoeff      = 1f;
        public const float WeaponBaseProjectileSpeed = 10f;
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
            _weaponData.baseProjectileSpeed = BalanceBaselines.WeaponBaseProjectileSpeed;
            _weaponData.levelCurves         = null; // explicit null → fallback path

            _classData = ScriptableObject.CreateInstance<ClassData>();
            _classData.baseHp        = (int)BalanceBaselines.WarriorBaseHp;
            _classData.basePower     = BalanceBaselines.WarriorBaseDamage;
            _classData.baseMoveSpeed = BalanceBaselines.WarriorBaseMoveSpeed;
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
        // Test 2: ResolveWeaponRuntimeProfile damage stays within ±20% of fixture
        //         for every level 1–5 when levelCurves is null (fallback path).
        //
        // Expected damage per level (fixture): playerDamage * powerCoeff
        //   level 1 → 10 * 1.0 = 10.0
        //   level 2 → 10 * 1.1 = 11.0  (GDD design intent; fallback returns same coeff)
        //   level 3 → 10 * 1.2 = 12.0
        //   level 4 → 10 * 1.3 = 13.0
        //   level 5 → 10 * 1.4 = 14.0
        //
        // Because levelCurves == null, all levels return basePowerCoeff = 1.0.
        // The actual damage will be playerDamage * 1.0 = 10.0 for every level.
        // We verify each result is within ±20% of the fixture expectation.
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
                Damage                   = BalanceBaselines.WarriorBaseDamage,
                CooldownMultiplier       = 1f,
                RangeMultiplier          = 1f,
                ProjectileSpeedMultiplier = 1f
            };

            WeaponRuntimeProfile result = RuntimeBalanceUtility.ResolveWeaponRuntimeProfile(
                _weaponData, level, player);

            float tolerance = expectedDamage * BalanceBaselines.Tolerance;
            Assert.That(result.Damage, Is.EqualTo(expectedDamage).Within(tolerance),
                $"Level {level}: damage {result.Damage} is outside ±20% of fixture {expectedDamage}");
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

                float hpTol    = BalanceBaselines.WarriorBaseHp        * BalanceBaselines.Tolerance;
                float dmgTol   = BalanceBaselines.WarriorBaseDamage    * BalanceBaselines.Tolerance;
                float speedTol = BalanceBaselines.WarriorBaseMoveSpeed * BalanceBaselines.Tolerance;

                Assert.That(profile.MaxHealth,  Is.EqualTo(BalanceBaselines.WarriorBaseHp).Within(hpTol),
                    "MaxHealth must be within ±20% of GDD baseline");
                Assert.That(profile.Damage,     Is.EqualTo(BalanceBaselines.WarriorBaseDamage).Within(dmgTol),
                    "Damage must be within ±20% of GDD baseline");
                Assert.That(profile.MoveSpeed,  Is.EqualTo(BalanceBaselines.WarriorBaseMoveSpeed).Within(speedTol),
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
}
