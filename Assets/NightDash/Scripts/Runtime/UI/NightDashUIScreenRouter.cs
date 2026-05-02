// Sprint B / M0 — UI screen state machine.
// Single source of truth for which top-level screen is visible. UI panels
// subscribe to OnScreenChanged and toggle their root GameObject.
// Stays static + event-based (no GameObject) so screens can be plain
// MonoBehaviours that listen at OnEnable.

using System;

namespace NightDash.Runtime.UI
{
    public enum NightDashUIScreen
    {
        Title = 0,
        Lobby,
        Playing,
        Paused,
        Result,
    }

    public static class NightDashUIScreenRouter
    {
        private static NightDashUIScreen _current = NightDashUIScreen.Title;
        private static NightDashUIScreen _previous = NightDashUIScreen.Title;

        public static NightDashUIScreen Current => _current;
        public static NightDashUIScreen Previous => _previous;

        // (previous, next)
        public static event Action<NightDashUIScreen, NightDashUIScreen> OnScreenChanged;

        public static void GoTo(NightDashUIScreen next)
        {
            if (_current == next) return;
            _previous = _current;
            _current = next;
            OnScreenChanged?.Invoke(_previous, _current);
        }

        // Returns to whatever screen was active before the current one.
        // No-op if no previous state recorded.
        public static void GoBack()
        {
            if (_previous == _current) return;
            GoTo(_previous);
        }

        // Test-only: clears state and unsubscribes all listeners.
        public static void Reset()
        {
            _current = NightDashUIScreen.Title;
            _previous = NightDashUIScreen.Title;
            OnScreenChanged = null;
        }
    }
}
