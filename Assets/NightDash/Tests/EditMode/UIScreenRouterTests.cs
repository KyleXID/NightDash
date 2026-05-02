// Sprint B / M0 — UIScreenRouter state transitions.
// Initial state, GoTo, GoBack, OnScreenChanged event firing semantics.

using NUnit.Framework;
using NightDash.Runtime.UI;

namespace NightDash.Tests.EditMode
{
    [TestFixture]
    public class UIScreenRouterTests
    {
        [SetUp]
        public void SetUp() => NightDashUIScreenRouter.Reset();

        [TearDown]
        public void TearDown() => NightDashUIScreenRouter.Reset();

        [Test]
        public void InitialState_IsTitle()
        {
            Assert.AreEqual(NightDashUIScreen.Title, NightDashUIScreenRouter.Current);
        }

        [Test]
        public void GoTo_ChangesCurrentAndPrevious()
        {
            NightDashUIScreenRouter.GoTo(NightDashUIScreen.Lobby);
            Assert.AreEqual(NightDashUIScreen.Lobby, NightDashUIScreenRouter.Current);
            Assert.AreEqual(NightDashUIScreen.Title, NightDashUIScreenRouter.Previous);
        }

        [Test]
        public void GoTo_SameScreen_DoesNotFireEvent()
        {
            int eventCount = 0;
            NightDashUIScreenRouter.OnScreenChanged += (_, __) => eventCount++;

            NightDashUIScreenRouter.GoTo(NightDashUIScreen.Title); // already Title
            Assert.AreEqual(0, eventCount);
        }

        [Test]
        public void GoTo_FiresEventWithBothScreens()
        {
            NightDashUIScreen capturedFrom = NightDashUIScreen.Result;
            NightDashUIScreen capturedTo = NightDashUIScreen.Result;
            NightDashUIScreenRouter.OnScreenChanged += (from, to) =>
            {
                capturedFrom = from;
                capturedTo = to;
            };

            NightDashUIScreenRouter.GoTo(NightDashUIScreen.Playing);

            Assert.AreEqual(NightDashUIScreen.Title, capturedFrom);
            Assert.AreEqual(NightDashUIScreen.Playing, capturedTo);
        }

        [Test]
        public void GoBack_ReturnsToPreviousScreen()
        {
            NightDashUIScreenRouter.GoTo(NightDashUIScreen.Lobby);
            NightDashUIScreenRouter.GoTo(NightDashUIScreen.Playing);
            NightDashUIScreenRouter.GoBack();
            Assert.AreEqual(NightDashUIScreen.Lobby, NightDashUIScreenRouter.Current);
        }

        [Test]
        public void TitleToLobbyToPlayingToPaused_CapturesSequence()
        {
            var path = new System.Collections.Generic.List<NightDashUIScreen>();
            NightDashUIScreenRouter.OnScreenChanged += (_, to) => path.Add(to);

            NightDashUIScreenRouter.GoTo(NightDashUIScreen.Lobby);
            NightDashUIScreenRouter.GoTo(NightDashUIScreen.Playing);
            NightDashUIScreenRouter.GoTo(NightDashUIScreen.Paused);

            CollectionAssert.AreEqual(
                new[] { NightDashUIScreen.Lobby, NightDashUIScreen.Playing, NightDashUIScreen.Paused },
                path);
        }
    }
}
