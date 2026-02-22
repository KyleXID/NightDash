using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace NightDash.Data
{
    [CreateAssetMenu(menuName = "NightDash/Data/Class Data", fileName = "class_")]
    public sealed class ClassData : ScriptableObject
    {
        [Header("Identity")]
        [FormerlySerializedAs("classId")] public string id;
        [FormerlySerializedAs("name")] public string displayName;
        public List<string> tags = new();

        [Header("Base Stats")]
        public int baseHp = 100;
        public float baseMoveSpeed = 5f;
        public float basePower = 10f;

        [Header("Loadout IDs")]
        public string startWeaponId;
        public string uniquePassiveId;
        public string ultimateSkillId;

        [Header("Legacy References (Optional)")]
        public WeaponData startingWeapon;
        public PassiveData startingPassive;
    }
}
