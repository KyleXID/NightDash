using NightDash.Data;
using NightDash.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NightDash.Editor
{
    public static class SceneDataRegistrySetup
    {
        private const string ScenePath = "Assets/Scenes/NightDashMain.unity";
        private const string CatalogPath = "Assets/NightDash/Data/data_catalog.asset";

        [MenuItem("NightDash/Scene/Attach DataRegistry To NightDashMain")]
        public static void AttachToNightDashMain()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var catalog = AssetDatabase.LoadAssetAtPath<DataCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError($"[NightDash] Missing catalog at '{CatalogPath}'. Run Data/Rebuild Data Catalog first.");
                return;
            }

            var registry = Object.FindFirstObjectByType<DataRegistry>();
            if (registry == null)
            {
                var go = new GameObject("DataRegistry");
                registry = go.AddComponent<DataRegistry>();
            }

            var serialized = new SerializedObject(registry);
            serialized.FindProperty("dataCatalog").objectReferenceValue = catalog;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(registry);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[NightDash] DataRegistry attached to NightDashMain and catalog assigned.");
        }
    }
}
