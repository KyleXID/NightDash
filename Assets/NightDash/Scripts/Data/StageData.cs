using UnityEngine;

namespace NightDash.Data
{
    [CreateAssetMenu(menuName = "NightDash/Data/Stage Data", fileName = "StageData")]
    public sealed class StageData : ScriptableObject
    {
        public string stageId;
        public string displayName;
        public float stageDurationSeconds = 900f;
        public float bossSpawnTimeSeconds = 900f;
        public int baseRewardPoints = 10;
    }
}
