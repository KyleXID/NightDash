using UnityEngine;

namespace NightDash.Data
{
    [CreateAssetMenu(menuName = "NightDash/Data/Weapon Data", fileName = "WeaponData")]
    public sealed class WeaponData : ScriptableObject
    {
        public string weaponId;
        public string displayName;
        public float baseDamage = 10f;
        public float cooldown = 1f;
        public float range = 5f;
        public bool canPierce;
    }
}
