// Sprint B / M0 — UI input polling.
// Edge-detect of arrow keys, confirm/cancel, pause. Polled in Update with
// unscaledTime so it keeps working while gameplay is time-paused.
// Static fields (set per-frame) let any UI panel read input without holding
// a reference. Auto-creates a single MonoBehaviour at scene load.

using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NightDash.Runtime.UI
{
    public sealed class NightDashUIInputRuntime : MonoBehaviour
    {
        public static bool LeftPressedThisFrame { get; private set; }
        public static bool RightPressedThisFrame { get; private set; }
        public static bool UpPressedThisFrame { get; private set; }
        public static bool DownPressedThisFrame { get; private set; }
        public static bool ConfirmPressedThisFrame { get; private set; }
        public static bool CancelPressedThisFrame { get; private set; }
        public static bool PausePressedThisFrame { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (FindAnyObjectByType<NightDashUIInputRuntime>() != null) return;
            var go = new GameObject("[NightDash] UIInputRuntime");
            go.AddComponent<NightDashUIInputRuntime>();
            DontDestroyOnLoad(go);
        }

        private void Update()
        {
            ResetFlags();

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;

            LeftPressedThisFrame = kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame;
            RightPressedThisFrame = kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame;
            UpPressedThisFrame = kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame;
            DownPressedThisFrame = kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame;
            ConfirmPressedThisFrame = kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
            CancelPressedThisFrame = kb.escapeKey.wasPressedThisFrame;
            // ESC doubles as both cancel and pause; consumers branch by context.
            PausePressedThisFrame = kb.escapeKey.wasPressedThisFrame;
#else
            LeftPressedThisFrame = Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
            RightPressedThisFrame = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
            UpPressedThisFrame = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
            DownPressedThisFrame = Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
            ConfirmPressedThisFrame = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
            CancelPressedThisFrame = Input.GetKeyDown(KeyCode.Escape);
            PausePressedThisFrame = Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        private static void ResetFlags()
        {
            LeftPressedThisFrame = false;
            RightPressedThisFrame = false;
            UpPressedThisFrame = false;
            DownPressedThisFrame = false;
            ConfirmPressedThisFrame = false;
            CancelPressedThisFrame = false;
            PausePressedThisFrame = false;
        }
    }
}
