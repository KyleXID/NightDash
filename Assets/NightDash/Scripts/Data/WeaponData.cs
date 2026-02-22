using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace NightDash.Data
{
    [CreateAssetMenu(menuName = "NightDash/Data/Weapon Data", fileName = "weapon_")]
    public sealed class WeaponData : ScriptableObject
    {
        [Header("Identity")]
        [FormerlySerializedAs("weaponId")] public string id;
        [FormerlySerializedAs("name")] public string displayName;
        public WeaponType weaponType = WeaponType.Projectile;

        [Header("Core")]
        [Min(1)] public int maxLevel = 8;
        [FormerlySerializedAs("cooldown")] public float baseCooldown = 1f;
        [FormerlySerializedAs("baseDamage")] public float basePowerCoeff = 1f;
        [FormerlySerializedAs("range")] public float baseRange = 5f;
        public float baseProjectileSpeed = 8f;

        [Header("Level Curves")]
        public List<WeaponLevelCurve> levelCurves = new();

        [Header("Flags")]
        public WeaponSpecialFlags specialFlags;

        public float GetPowerCoeffOrDefault(int level)
        {
            foreach (var curve in levelCurves)
            {
                if (curve.level == level)
                {
                    return curve.powerCoeff;
                }
            }

            return basePowerCoeff;
        }
    }
}
