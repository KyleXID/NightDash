using UnityEngine;

namespace NightDash.Data
{
    [CreateAssetMenu(menuName = "NightDash/Data/Meta Tree Data")]
    public class MetaTreeData : ScriptableObject
    {
        public string classId;
        public int attackNodeCost = 50;
        public int survivalNodeCost = 50;
        public int abyssNodeCost = 100;
        public float attackBonusPerNode = 0.05f;
        public float survivalBonusPerNode = 0.05f;
        public float abyssBonusPerNode = 0.03f;
    }
}
