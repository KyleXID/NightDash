using UnityEngine;

namespace NightDash.Runtime
{
    public sealed class NightDashRuntimeToggles : MonoBehaviour
    {
        [Header("Bootstrap")]
        [SerializeField] private bool enableFallbackBootstrapWhenBakingMissing = true;

        [Header("Logging")]
        // S4-07: 릴리스 빌드에서 게임 상태 문자열이 Debug.Log로 새지 않도록 기본값 false.
        // 개발 시에는 씬 Inspector에서 true로 토글.
        [SerializeField] private bool verboseRuntimeLogs = false;

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
