using UnityEngine;

namespace NightDash.Data
{
    [CreateAssetMenu(menuName = "NightDash/Data/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        public string weaponId;
        public string displayName;
        public float baseDamage = 10f;
        public float cooldown = 1.2f;
        public float projectileSpeed = 8f;
        public float range = 6f;
        public int maxLevel = 8;
    }
}
