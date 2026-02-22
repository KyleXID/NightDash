using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace NightDash.Data
{
    [CreateAssetMenu(menuName = "NightDash/Data/Stage Data", fileName = "stage_")]
    public sealed class StageData : ScriptableObject
    {
        [Header("Identity")]
        [FormerlySerializedAs("stageId")] public string id;
        [FormerlySerializedAs("name")] public string displayName;

        [Header("Timeline")]
        [FormerlySerializedAs("stageDurationSeconds")] public int durationSec = 900;
        [FormerlySerializedAs("bossSpawnTimeSeconds")] public int bossSpawnSec = 900;

        [Header("Map Bounds")]
        public bool useBounds = true;
        public Vector2 boundsCenter = Vector2.zero;
        public Vector2 boundsSize = new Vector2(60f, 36f);

        [Header("Content")]
        public List<string> environmentHazards = new();
        public List<SpawnPhase> spawnPhases = new();
        public List<EliteEvent> eliteEvents = new();

        [Header("Boss/Reward")]
        public string bossId;
        public string rewardTableId;
        [FormerlySerializedAs("baseRewardPoints")] public int baseRewardPoints = 10;
    }
}
