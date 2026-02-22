using System.Collections.Generic;
using NightDash.Data;
using UnityEngine;

namespace NightDash.Runtime
{
    public sealed class DataRegistry : MonoBehaviour
    {
        public static DataRegistry Instance { get; private set; }

        [SerializeField] private DataCatalog dataCatalog;

        private readonly Dictionary<string, ClassData> _classById = new();
        private readonly Dictionary<string, WeaponData> _weaponById = new();
        private readonly Dictionary<string, PassiveData> _passiveById = new();
        private readonly Dictionary<string, EvolutionData> _evolutionById = new();
        private readonly Dictionary<string, StageData> _stageById = new();
        private readonly Dictionary<string, DifficultyModifierData> _difficultyById = new();
        private readonly Dictionary<string, MetaTreeData> _metaTreeByClassId = new();

        public DataCatalog Catalog => dataCatalog;

        public void SetCatalog(DataCatalog catalog)
        {
            dataCatalog = catalog;
            RebuildLookup();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            RebuildLookup();
        }

        public void RebuildLookup()
        {
            _classById.Clear();
            _weaponById.Clear();
            _passiveById.Clear();
            _evolutionById.Clear();
            _stageById.Clear();
            _difficultyById.Clear();
            _metaTreeByClassId.Clear();

            if (dataCatalog == null)
            {
                Debug.LogWarning("[NightDash] DataCatalog is not assigned.");
                return;
            }

            AddRange(dataCatalog.classes, _classById, x => x != null ? x.id : null, "ClassData");
            AddRange(dataCatalog.weapons, _weaponById, x => x != null ? x.id : null, "WeaponData");
            AddRange(dataCatalog.passives, _passiveById, x => x != null ? x.id : null, "PassiveData");
            AddRange(dataCatalog.evolutions, _evolutionById, x => x != null ? x.id : null, "EvolutionData");
            AddRange(dataCatalog.stages, _stageById, x => x != null ? x.id : null, "StageData");
            AddRange(dataCatalog.difficultyModifiers, _difficultyById, x => x != null ? x.id : null, "DifficultyModifierData");
            AddRange(dataCatalog.metaTrees, _metaTreeByClassId, x => x != null ? x.classId : null, "MetaTreeData");

            Debug.Log(
                $"[NightDash] DataRegistry ready: classes={_classById.Count}, weapons={_weaponById.Count}, passives={_passiveById.Count}, " +
                $"evolutions={_evolutionById.Count}, stages={_stageById.Count}, difficulty={_difficultyById.Count}, metaTrees={_metaTreeByClassId.Count}");
        }

        public bool TryGetClass(string id, out ClassData value) => _classById.TryGetValue(id, out value);
        public bool TryGetWeapon(string id, out WeaponData value) => _weaponById.TryGetValue(id, out value);
        public bool TryGetPassive(string id, out PassiveData value) => _passiveById.TryGetValue(id, out value);
        public bool TryGetEvolution(string id, out EvolutionData value) => _evolutionById.TryGetValue(id, out value);
        public bool TryGetStage(string id, out StageData value) => _stageById.TryGetValue(id, out value);
        public bool TryGetDifficulty(string id, out DifficultyModifierData value) => _difficultyById.TryGetValue(id, out value);
        public bool TryGetMetaTree(string classId, out MetaTreeData value) => _metaTreeByClassId.TryGetValue(classId, out value);

        private static void AddRange<T>(
            List<T> source,
            Dictionary<string, T> target,
            System.Func<T, string> keySelector,
            string typeName)
            where T : Object
        {
            if (source == null)
            {
                return;
            }

            foreach (var item in source)
            {
                var key = keySelector(item);
                if (item == null || string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (!target.TryAdd(key, item))
                {
                    Debug.LogWarning($"[NightDash] Duplicate {typeName} key '{key}' in DataCatalog.");
                }
            }
        }
    }
}
