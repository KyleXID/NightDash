using UnityEngine;

namespace NightDash.Data
{
    [CreateAssetMenu(menuName = "NightDash/Data/Class Data")]
    public class ClassData : ScriptableObject
    {
        public string classId;
        public string displayName;
        public WeaponData startingWeapon;
        public PassiveData startingPassive;
        public float baseHealth = 100f;
        public float baseMoveSpeed = 4f;
        public float baseDamage = 10f;
    }
}
