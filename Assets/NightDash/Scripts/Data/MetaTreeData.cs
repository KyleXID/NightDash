using System.Collections.Generic;
using UnityEngine;

namespace NightDash.Data
{
    [CreateAssetMenu(menuName = "NightDash/Data/Meta Tree Data", fileName = "meta_tree_")]
    public sealed class MetaTreeData : ScriptableObject
    {
        public string classId;
        public List<MetaNodeData> nodes = new();
    }
}
