// Sprint B / M3 — Pause Menu input context flow.
// Verifies the Lobby.StartRun / Lobby.BackToTitle hand-off patterns and
// ESC toggle idempotency that protect the Pause Menu from silent failures.

using NUnit.Framework;
using NightDash.Runtime.UI;

namespace NightDash.Tests.EditMode
{
    [TestFixture]
    public class PauseMenuFlowTests
    {
        [SetUp]
        public void SetUp() => NightDashInputContextStack.Reset();

        // Mirrors Lobby.StartRun's hand-off: explicit Pop(Lobby) before
        // Push(Playing) so the stack ends with [Playing]. Without the Push
        // the Pause Menu's ESC trigger (which gates on Top == Playing)
        // would never fire — silent failure.
        [Test]
        public void LobbyStartRun_HandsOffToPlaying()
        {
            NightDashInputContextStack.Push(NightDashInputContext.Lobby);

            Assert.IsTrue(NightDashInputContextStack.Pop(NightDashInputContext.Lobby));
            NightDashInputContextStack.Push(NightDashInputContext.Playing);

            Assert.AreEqual(NightDashInputContext.Playing, NightDashInputContextStack.Top);
            Assert.AreEqual(1, NightDashInputContextStack.Count);
            Assert.IsFalse(NightDashInputContextStack.Contains(NightDashInputContext.Lobby));
        }

        // Mirrors Lobby.BackToTitle's hand-off: explicit Pop(Lobby) before
        // Title.OnEnable's Push(Title). If Pop is delayed until OnDisable,
        // Top would already be Title and the Pop would silently fail,
        // leaving Lobby leaked underneath Title.
        [Test]
        public void LobbyBackToTitle_HandsOffToTitle()
        {
            NightDashInputContextStack.Push(NightDashInputContext.Lobby);

            Assert.IsTrue(NightDashInputContextStack.Pop(NightDashInputContext.Lobby));
            NightDashInputContextStack.Push(NightDashInputContext.Title);

            Assert.AreEqual(NightDashInputContext.Title, NightDashInputContextStack.Top);
            Assert.AreEqual(1, NightDashInputContextStack.Count);
            Assert.IsFalse(NightDashInputContextStack.Contains(NightDashInputContext.Lobby));
        }

        // ESC double-toggle stress: 100 open/close cycles must leave the
        // stack at exactly [Playing] with zero drift.
        [Test]
        public void EscToggle_NoStackDriftOver100Cycles()
        {
            NightDashInputContextStack.Push(NightDashInputContext.Playing);

            for (int i = 0; i < 100; i++)
            {
                NightDashInputContextStack.Push(NightDashInputContext.Pause);
                Assert.IsTrue(NightDashInputContextStack.Pop(NightDashInputContext.Pause));
            }

            Assert.AreEqual(NightDashInputContext.Playing, NightDashInputContextStack.Top);
            Assert.AreEqual(1, NightDashInputContextStack.Count);
        }

        // Lobby.OnDisable's Pop(Lobby) running while Pause is on top must
        // be a silent no-op — that's the guard that lets StartRun/BackToTitle
        // pop manually before disable without double-popping.
        [Test]
        public void OnDisablePopLobby_SilentlyNoOpsWhenChildContextOnTop()
        {
            // Stack after StartRun's prologue:
            NightDashInputContextStack.Push(NightDashInputContext.Playing);
            int before = NightDashInputContextStack.Count;
            var topBefore = NightDashInputContextStack.Top;

            // Lobby.OnDisable runs and tries Pop(Lobby).
            Assert.IsFalse(NightDashInputContextStack.Pop(NightDashInputContext.Lobby));

            Assert.AreEqual(before, NightDashInputContextStack.Count);
            Assert.AreEqual(topBefore, NightDashInputContextStack.Top);
        }

        // Pause Menu's Return-to-Lobby path: Pop(Pause) + Pop(Playing) +
        // Lobby.OnEnable Push(Lobby) leaves the stack at [Lobby].
        [Test]
        public void PauseReturnToLobby_RestoresLobbyContext()
        {
            NightDashInputContextStack.Push(NightDashInputContext.Playing);
            NightDashInputContextStack.Push(NightDashInputContext.Pause);

            Assert.IsTrue(NightDashInputContextStack.Pop(NightDashInputContext.Pause));
            Assert.IsTrue(NightDashInputContextStack.Pop(NightDashInputContext.Playing));
            NightDashInputContextStack.Push(NightDashInputContext.Lobby);

            Assert.AreEqual(NightDashInputContext.Lobby, NightDashInputContextStack.Top);
            Assert.AreEqual(1, NightDashInputContextStack.Count);
        }
    }
}
