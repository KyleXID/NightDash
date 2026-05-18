using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace NightDash.Data
{
    [CreateAssetMenu(menuName = "NightDash/Data/Passive Data", fileName = "passive_")]
    public sealed class PassiveData : ScriptableObject
    {
        [Header("Identity")]
        [FormerlySerializedAs("passiveId")] public string id;
        [FormerlySerializedAs("name")] public string displayName;
        [TextArea] public string description;
        // Optional explicit icon override. When null the runtime falls back
        // to a Resources lookup keyed on the passive id (see
        // NightDashUIIcons.GetPassive).
        public Sprite icon;

        [Header("Config")]
        public PassiveCategory category = PassiveCategory.Stat;
        [Min(1)] public int maxLevel = 5;

        [Header("Effects")]
        public List<PassiveEffect> effects = new();
        public PassiveCondition condition;

        [Header("Legacy Multipliers (Optional)")]
        [FormerlySerializedAs("healthMultiplier")] public float legacyHealthMultiplier = 1f;
        [FormerlySerializedAs("damageMultiplier")] public float legacyDamageMultiplier = 1f;
        [FormerlySerializedAs("cooldownMultiplier")] public float legacyCooldownMultiplier = 1f;
    }
}
