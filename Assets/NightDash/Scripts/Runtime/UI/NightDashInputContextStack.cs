// Sprint B / M0 — Input context stack.
// All UI/gameplay code routes input by querying the topmost context so ESC,
// F1, and confirm/cancel keys never collide across menus, pause, and tutorial.
// Static (Unity main-thread only); no GameObject required.

using System.Collections.Generic;

namespace NightDash.Runtime.UI
{
    public enum NightDashInputContext
    {
        None = 0,
        Title,
        Lobby,
        Playing,
        Pause,
        Tutorial,
        Result,
    }

    public static class NightDashInputContextStack
    {
        private static readonly Stack<NightDashInputContext> _stack = new();

        public static NightDashInputContext Top
        {
            get
            {
                if (_stack.Count == 0) return NightDashInputContext.None;
                return _stack.Peek();
            }
        }

        public static int Count => _stack.Count;

        public static void Push(NightDashInputContext ctx)
        {
            _stack.Push(ctx);
        }

        // Pops only when the topmost matches the expected context.
        // Prevents accidental pop when control flow is asymmetric.
        public static bool Pop(NightDashInputContext expected)
        {
            if (_stack.Count == 0) return false;
            if (_stack.Peek() != expected) return false;
            _stack.Pop();
            return true;
        }

        // Unconditional pop. Use only for emergency reset paths.
        public static bool PopAny()
        {
            if (_stack.Count == 0) return false;
            _stack.Pop();
            return true;
        }

        public static void Reset()
        {
            _stack.Clear();
        }

        public static bool Contains(NightDashInputContext ctx)
        {
            foreach (var c in _stack)
            {
                if (c == ctx) return true;
            }
            return false;
        }

        // True only when the topmost matches; the canonical "should I handle
        // this input?" check.
        public static bool IsActive(NightDashInputContext ctx)
        {
            return Top == ctx;
        }
    }
}
