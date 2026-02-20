using UnityEngine;

namespace NightDash.Data
{
    [CreateAssetMenu(menuName = "NightDash/Data/Difficulty Modifier Data", fileName = "DifficultyModifierData")]
    public sealed class DifficultyModifierData : ScriptableObject
    {
        public string modifierId;
        public string displayName;
        public int riskScore = 1;
        public float rewardMultiplierBonus = 0.1f;
    }
}
