using UnityEngine;

namespace NightDash.Data
{
    [CreateAssetMenu(menuName = "NightDash/Data/Class Data", fileName = "ClassData")]
    public sealed class ClassData : ScriptableObject
    {
        [Header("Identity")]
        public string classId;
        public string displayName;

        [Header("Runtime")]
        public WeaponData startingWeapon;
        public PassiveData startingPassive;
        public float baseHealth = 100f;
        public float baseMoveSpeed = 5f;
    }
}
