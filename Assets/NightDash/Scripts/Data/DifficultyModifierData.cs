using UnityEngine;
using UnityEngine.Serialization;

namespace NightDash.Data
{
    [CreateAssetMenu(menuName = "NightDash/Data/Difficulty Modifier Data", fileName = "difficulty_")]
    public sealed class DifficultyModifierData : ScriptableObject
    {
        [Header("Identity")]
        [FormerlySerializedAs("modifierId")] public string id;
        [FormerlySerializedAs("name")] public string displayName;

        [Header("Classification")]
        public DifficultyCategory category = DifficultyCategory.Combat;
        [FormerlySerializedAs("riskScore")] public int riskPoint = 1;

        [Header("Modifiers")]
        public EnemyModifierValues enemyModifiers;
        public PlayerModifierValues playerModifiers;
        public RuntimeEffectValues runtimeEffects;

        [Header("Reward")]
        [FormerlySerializedAs("rewardMultiplierBonus")] public float rewardBonusPct = 0.1f;
    }
}
