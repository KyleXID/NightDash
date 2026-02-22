using NightDash.Data;
using NightDash.ECS.Authoring;
using NightDash.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NightDash.Editor
{
    public static class NightDashBootstrapSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string CatalogPath = "Assets/NightDash/Data/data_catalog.asset";

        [MenuItem("NightDash/Scene/Setup SampleScene Bootstrap")]
        public static void SetupSampleSceneBootstrap()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[NightDash] Stop Play Mode first, then run Scene/Setup SampleScene Bootstrap.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var catalog = AssetDatabase.LoadAssetAtPath<DataCatalog>(CatalogPath);
            var mainCamera = Camera.main;

            if (catalog == null)
            {
                Debug.LogError($"[NightDash] Missing catalog at '{CatalogPath}'. Run NightDash/Data/Rebuild Data Catalog first.");
                return;
            }

            var bootstrap = Object.FindFirstObjectByType<NightDashBootstrapAuthoring>();
            if (bootstrap == null)
            {
                var bootstrapGo = new GameObject("NightDashBootstrap");
                bootstrap = bootstrapGo.AddComponent<NightDashBootstrapAuthoring>();
            }
            bootstrap.TryAutoAssignSpawnPrefabs();

            var registry = Object.FindFirstObjectByType<DataRegistry>();
            if (registry == null)
            {
                var registryGo = new GameObject("DataRegistry");
                registry = registryGo.AddComponent<DataRegistry>();
            }

            var lobbyUi = Object.FindFirstObjectByType<RunSelectionLobbyUI>();
            if (lobbyUi == null)
            {
                var uiGo = new GameObject("RunSelectionLobbyUI");
                lobbyUi = uiGo.AddComponent<RunSelectionLobbyUI>();
            }

            var inputRuntime = Object.FindFirstObjectByType<NightDashPlayerInputRuntime>();
            if (inputRuntime == null)
            {
                var inputGo = new GameObject("NightDashPlayerInputRuntime");
                inputRuntime = inputGo.AddComponent<NightDashPlayerInputRuntime>();
            }

            var visualBridge = Object.FindFirstObjectByType<NightDashDebugVisualBridge>();
            if (visualBridge == null)
            {
                var viewGo = new GameObject("NightDashDebugVisualBridge");
                visualBridge = viewGo.AddComponent<NightDashDebugVisualBridge>();
            }

            if (mainCamera != null && mainCamera.GetComponent<NightDashCameraFollow>() == null)
            {
                mainCamera.gameObject.AddComponent<NightDashCameraFollow>();
                EditorUtility.SetDirty(mainCamera.gameObject);
            }

            var runtimeToggles = Object.FindFirstObjectByType<NightDashRuntimeToggles>();
            if (runtimeToggles == null)
            {
                var togglesGo = new GameObject("NightDashRuntimeToggles");
                runtimeToggles = togglesGo.AddComponent<NightDashRuntimeToggles>();
            }

            var serialized = new SerializedObject(registry);
            serialized.FindProperty("dataCatalog").objectReferenceValue = catalog;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(bootstrap);
            EditorUtility.SetDirty(registry);
            EditorUtility.SetDirty(lobbyUi);
            EditorUtility.SetDirty(inputRuntime);
            EditorUtility.SetDirty(visualBridge);
            EditorUtility.SetDirty(runtimeToggles);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[NightDash] SampleScene bootstrap setup completed (NightDashBootstrap + DataRegistry + RunSelectionLobbyUI + PlayerInputRuntime + CameraFollow + DebugVisualBridge + RuntimeToggles + catalog).");
        }
    }
}
