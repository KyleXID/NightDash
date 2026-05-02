// Sprite animation pipeline — frame index math + SO clip lookup invariants.
// EditMode-only: no Sprite assets needed; ComputeFrameIndex is pure.

using NUnit.Framework;
using UnityEngine;
using NightDash.Data;

namespace NightDash.Tests.EditMode
{
    [TestFixture]
    public class SpriteAnimationSetTests
    {
        // ---------------- ComputeFrameIndex (pure) ----------------

        [Test]
        public void ComputeFrameIndex_EmptyFrames_ReturnsZero()
        {
            Assert.AreEqual(0, AnimationClipDef.ComputeFrameIndex(0.5f, 0, 12f, true));
            Assert.AreEqual(0, AnimationClipDef.ComputeFrameIndex(0.5f, 0, 12f, false));
        }

        [Test]
        public void ComputeFrameIndex_NonPositiveFps_ReturnsZero()
        {
            Assert.AreEqual(0, AnimationClipDef.ComputeFrameIndex(1f, 8, 0f, true));
            Assert.AreEqual(0, AnimationClipDef.ComputeFrameIndex(1f, 8, -5f, true));
        }

        [Test]
        public void ComputeFrameIndex_LoopWrapsForward()
        {
            // 8 frames at 12 fps: t=0 → 0, t=8/12 → 0 (wrapped), t=9/12 → 1
            Assert.AreEqual(0, AnimationClipDef.ComputeFrameIndex(0f, 8, 12f, true));
            Assert.AreEqual(7, AnimationClipDef.ComputeFrameIndex(7f / 12f, 8, 12f, true));
            Assert.AreEqual(0, AnimationClipDef.ComputeFrameIndex(8f / 12f, 8, 12f, true));
            Assert.AreEqual(1, AnimationClipDef.ComputeFrameIndex(9f / 12f, 8, 12f, true));
        }

        [Test]
        public void ComputeFrameIndex_LoopHandlesNegativeTime()
        {
            // Defensive: negative time should not produce negative index
            int idx = AnimationClipDef.ComputeFrameIndex(-0.1f, 8, 12f, true);
            Assert.GreaterOrEqual(idx, 0);
            Assert.Less(idx, 8);
        }

        [Test]
        public void ComputeFrameIndex_NoLoopClampsAtLast()
        {
            Assert.AreEqual(7, AnimationClipDef.ComputeFrameIndex(100f, 8, 12f, false));
            Assert.AreEqual(0, AnimationClipDef.ComputeFrameIndex(-100f, 8, 12f, false));
        }

        // ---------------- SpriteAnimationSetSO clip lookup ----------------

        [Test]
        public void TryGetClip_FindsByName()
        {
            var so = ScriptableObject.CreateInstance<SpriteAnimationSetSO>();
            try
            {
                so.id = "test_id";
                so.clips.Add(new AnimationClipDef { name = "Walk", fps = 12f, loop = true });
                so.clips.Add(new AnimationClipDef { name = "Idle", fps = 6f, loop = true });

                Assert.IsTrue(so.TryGetClip("Walk", out var walk));
                Assert.AreEqual(12f, walk.fps);

                Assert.IsTrue(so.TryGetClip("Idle", out var idle));
                Assert.AreEqual(6f, idle.fps);

                Assert.IsFalse(so.TryGetClip("Death", out _));
            }
            finally
            {
                Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void GetClipOrFallback_PrefersPrimary_FallsBackToSecondary_ThenFirst()
        {
            var so = ScriptableObject.CreateInstance<SpriteAnimationSetSO>();
            try
            {
                var walk = new AnimationClipDef { name = "Walk" };
                var idle = new AnimationClipDef { name = "Idle" };
                so.clips.Add(walk);
                so.clips.Add(idle);

                Assert.AreSame(idle, so.GetClipOrFallback("Idle", "Walk"));
                Assert.AreSame(walk, so.GetClipOrFallback("MissingClip", "Walk"));
                // Both missing → first clip
                Assert.AreSame(walk, so.GetClipOrFallback("X", "Y"));
            }
            finally
            {
                Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void GetClipOrFallback_EmptyClipsReturnsNull()
        {
            var so = ScriptableObject.CreateInstance<SpriteAnimationSetSO>();
            try
            {
                Assert.IsNull(so.GetClipOrFallback("Walk", "Idle"));
            }
            finally
            {
                Object.DestroyImmediate(so);
            }
        }
    }
}
