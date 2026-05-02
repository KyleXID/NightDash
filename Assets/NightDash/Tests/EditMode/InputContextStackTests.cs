// Sprint B / M0 — InputContextStack invariants.
// Push/pop sequencing, asymmetric pop guard, top-of-stack routing.

using NUnit.Framework;
using NightDash.Runtime.UI;

namespace NightDash.Tests.EditMode
{
    [TestFixture]
    public class InputContextStackTests
    {
        [SetUp]
        public void SetUp() => NightDashInputContextStack.Reset();

        [Test]
        public void Empty_TopReturnsNone()
        {
            Assert.AreEqual(NightDashInputContext.None, NightDashInputContextStack.Top);
            Assert.AreEqual(0, NightDashInputContextStack.Count);
        }

        [Test]
        public void Push_TopReturnsLatest()
        {
            NightDashInputContextStack.Push(NightDashInputContext.Title);
            Assert.AreEqual(NightDashInputContext.Title, NightDashInputContextStack.Top);

            NightDashInputContextStack.Push(NightDashInputContext.Lobby);
            Assert.AreEqual(NightDashInputContext.Lobby, NightDashInputContextStack.Top);
            Assert.AreEqual(2, NightDashInputContextStack.Count);
        }

        [Test]
        public void Pop_OnlyPopsWhenExpectedMatches()
        {
            NightDashInputContextStack.Push(NightDashInputContext.Playing);
            NightDashInputContextStack.Push(NightDashInputContext.Pause);

            // Mismatched expectation should NOT pop.
            Assert.IsFalse(NightDashInputContextStack.Pop(NightDashInputContext.Tutorial));
            Assert.AreEqual(NightDashInputContext.Pause, NightDashInputContextStack.Top);

            // Matched expectation pops.
            Assert.IsTrue(NightDashInputContextStack.Pop(NightDashInputContext.Pause));
            Assert.AreEqual(NightDashInputContext.Playing, NightDashInputContextStack.Top);
        }

        [Test]
        public void Pop_OnEmptyReturnsFalse()
        {
            Assert.IsFalse(NightDashInputContextStack.Pop(NightDashInputContext.Title));
        }

        [Test]
        public void IsActive_OnlyMatchesTopmost()
        {
            NightDashInputContextStack.Push(NightDashInputContext.Lobby);
            NightDashInputContextStack.Push(NightDashInputContext.Pause);

            Assert.IsTrue(NightDashInputContextStack.IsActive(NightDashInputContext.Pause));
            Assert.IsFalse(NightDashInputContextStack.IsActive(NightDashInputContext.Lobby));
        }

        [Test]
        public void Contains_FindsAnywhereInStack()
        {
            NightDashInputContextStack.Push(NightDashInputContext.Playing);
            NightDashInputContextStack.Push(NightDashInputContext.Tutorial);
            NightDashInputContextStack.Push(NightDashInputContext.Pause);

            Assert.IsTrue(NightDashInputContextStack.Contains(NightDashInputContext.Playing));
            Assert.IsTrue(NightDashInputContextStack.Contains(NightDashInputContext.Tutorial));
            Assert.IsTrue(NightDashInputContextStack.Contains(NightDashInputContext.Pause));
            Assert.IsFalse(NightDashInputContextStack.Contains(NightDashInputContext.Lobby));
        }

        [Test]
        public void TypicalFlow_GameplayThenPauseThenResume()
        {
            // Enter gameplay
            NightDashInputContextStack.Push(NightDashInputContext.Playing);
            Assert.AreEqual(NightDashInputContext.Playing, NightDashInputContextStack.Top);

            // ESC opens pause
            NightDashInputContextStack.Push(NightDashInputContext.Pause);
            Assert.AreEqual(NightDashInputContext.Pause, NightDashInputContextStack.Top);

            // ESC again resumes
            Assert.IsTrue(NightDashInputContextStack.Pop(NightDashInputContext.Pause));
            Assert.AreEqual(NightDashInputContext.Playing, NightDashInputContextStack.Top);
        }
    }
}
