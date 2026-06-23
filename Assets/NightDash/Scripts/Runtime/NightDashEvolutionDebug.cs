// ============================================================================
// NightDashEvolutionDebug.cs
// Dev-only quick trigger for weapon evolution. Press F8 to force every owned
// weapon to its first-tier ("_evolved") form, ignoring the normal max-level /
// max-passive conditions — for fast VFX & mechanics iteration without having to
// grind a weapon and its passives to max in-run.
//
// The input reader (NightDashEvolutionDebugInput) is compiled only in the Editor
// and development builds. EvolutionDebug itself is always compiled so the ECS
// EvolutionSystem can read the flag unconditionally; in release builds nothing
// ever sets it, so ConsumeForceRequest() always returns false.
// ============================================================================

using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif

namespace NightDash.Runtime
{
    // One-shot request flag shared between the dev input reader and the ECS
    // EvolutionSystem. Set on key press, consumed (and cleared) by the system.
    internal static class EvolutionDebug
    {
        private static bool _forceRequested;

        public static void RequestForce() => _forceRequested = true;

        public static bool ConsumeForceRequest()
        {
            if (!_forceRequested) return false;
            _forceRequested = false;
            return true;
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public sealed class NightDashEvolutionDebugInput : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (FindAnyObjectByType<NightDashEvolutionDebugInput>() != null) return;

            var go = new GameObject("[NightDash] EvolutionDebugInput");
            go.AddComponent<NightDashEvolutionDebugInput>();
            DontDestroyOnLoad(go);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f8Key.wasPressedThisFrame)
            {
                EvolutionDebug.RequestForce();
                NightDashLog.Info("[EvolutionDebug] Force-evolve requested (F8).");
            }
        }
    }
#endif
}
