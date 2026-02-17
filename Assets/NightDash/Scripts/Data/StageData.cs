using UnityEngine;

namespace NightDash.Data
{
    [CreateAssetMenu(menuName = "NightDash/Data/Stage Data")]
    public class StageData : ScriptableObject
    {
        public int stageIndex;
        public string stageName;
        public float clearTimeSeconds = 900f;
        public float bossSpawnTimeSeconds = 900f;
        public int baseConquestPointReward = 100;
    }
}
