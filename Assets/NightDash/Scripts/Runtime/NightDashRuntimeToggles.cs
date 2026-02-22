using UnityEngine;

namespace NightDash.Runtime
{
    public sealed class NightDashRuntimeToggles : MonoBehaviour
    {
        [Header("Bootstrap")]
        [SerializeField] private bool enableFallbackBootstrapWhenBakingMissing = true;

        [Header("Logging")]
        [SerializeField] private bool verboseRuntimeLogs = true;

        private static NightDashRuntimeToggles _instance;

        public static bool EnableFallbackBootstrapWhenBakingMissing =>
            _instance == null || _instance.enableFallbackBootstrapWhenBakingMissing;

        public static bool VerboseRuntimeLogs => _instance != null && _instance.verboseRuntimeLogs;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoCreateIfMissing()
        {
            if (_instance != null)
            {
                return;
            }

            var existing = FindFirstObjectByType<NightDashRuntimeToggles>(FindObjectsInactive.Include);
            if (existing != null)
            {
                _instance = existing;
                return;
            }

            var go = new GameObject("NightDashRuntimeToggles");
            _instance = go.AddComponent<NightDashRuntimeToggles>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
}
