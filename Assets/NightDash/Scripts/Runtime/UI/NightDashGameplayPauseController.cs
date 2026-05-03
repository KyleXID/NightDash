// Sprint B / M3 — Gameplay-context ESC trigger for the Pause Menu.
// Sits as an always-active MonoBehaviour because NightDashPauseMenuUI is
// inactive between pause sessions and cannot listen for its own activation
// key. This controller scans ESC every frame and only acts while the input
// stack's top is Playing — Title/Lobby cancel keys remain isolated.

using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NightDash.Runtime.UI
{
    public sealed class NightDashGameplayPauseController : MonoBehaviour
    {
        private NightDashPauseMenuUI _pauseMenu;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreateIfMissing()
        {
            var existing = FindFirstObjectByType<NightDashGameplayPauseController>(FindObjectsInactive.Include);
            if (existing != null) return;

            var go = new GameObject("NightDashGameplayPauseController");
            go.AddComponent<NightDashGameplayPauseController>();
            // Persist so a single controller services any scene transitions.
            DontDestroyOnLoad(go);
        }

        private void Update()
        {
            // Only the Playing context opens the pause menu via ESC. Title
            // and Lobby use ESC for their own cancel/back semantics, which
            // are guarded by their own Top-context checks.
            if (NightDashInputContextStack.Top != NightDashInputContext.Playing) return;

            if (!IsEscapePressedThisFrame()) return;

            if (_pauseMenu == null)
            {
                _pauseMenu = FindFirstObjectByType<NightDashPauseMenuUI>(FindObjectsInactive.Include);
                if (_pauseMenu == null) return;
            }

            // SetActive(true) cascades through OnEnable, which performs the
            // full pause hand-off: Push(Pause) + GoTo(Paused) + timeScale=0
            // + GameplayPauseTag entity creation.
            _pauseMenu.gameObject.SetActive(true);
        }

        private static bool IsEscapePressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }
    }
}
