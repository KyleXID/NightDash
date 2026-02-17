using UnityEngine;

namespace NightDash.Data
{
    [CreateAssetMenu(menuName = "NightDash/Data/Difficulty Modifier Data")]
    public class DifficultyModifierData : ScriptableObject
    {
        public string modifierId;
        public string displayName;
        [TextArea] public string description;
        public float riskScore = 10f;
        public float rewardMultiplier = 1.1f;
    }
}
