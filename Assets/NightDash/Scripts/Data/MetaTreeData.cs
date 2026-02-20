using UnityEngine;

namespace NightDash.Data
{
    [CreateAssetMenu(menuName = "NightDash/Data/Meta Tree Data", fileName = "MetaTreeData")]
    public sealed class MetaTreeData : ScriptableObject
    {
        public string classId;
        public int attackNodeCount = 5;
        public int survivalNodeCount = 5;
        public int abyssNodeCount = 5;
    }
}
