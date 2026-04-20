// S3-05: AudioLibrary 슬롯 구조 테스트.
// 런타임 할당 없이 enum·슬롯 수·Resolve 분기가 GDD 8 이벤트와 일치하는지 회귀.

using NUnit.Framework;
using UnityEngine;
using NightDash.Data;

namespace NightDash.Tests.EditMode
{
    [TestFixture]
    public class AudioLibraryTests
    {
        private AudioLibrary _library;

        [OneTimeSetUp]
        public void Setup()
        {
            _library = ScriptableObject.CreateInstance<AudioLibrary>();
        }

        [OneTimeTearDown]
        public void Teardown()
        {
            if (_library != null)
            {
                Object.DestroyImmediate(_library);
            }
        }

        [Test]
        public void AudioEventId_Has_Exactly_Eight_Values()
        {
            // GDD 필수 이벤트 8종 고정. 추가/삭제 시 본 테스트와 AudioLibrary 양쪽을 함께 수정.
            Assert.That(System.Enum.GetValues(typeof(AudioEventId)).Length, Is.EqualTo(8));
        }

        [TestCase(AudioEventId.RunStart)]
        [TestCase(AudioEventId.RunVictory)]
        [TestCase(AudioEventId.RunDefeat)]
        [TestCase(AudioEventId.LevelUp)]
        [TestCase(AudioEventId.ChestOpen)]
        [TestCase(AudioEventId.EvolutionTrigger)]
        [TestCase(AudioEventId.BossSpawn)]
        [TestCase(AudioEventId.BossKill)]
        public void Resolve_Returns_Null_Placeholder_When_Clip_Not_Assigned(AudioEventId eventId)
        {
            // 플레이스홀더 단계에서는 모든 슬롯이 null — Resolve는 null을 반환해야 함 (예외 없이).
            AudioClip clip = _library.Resolve(eventId);
            Assert.That(clip, Is.Null);
        }

        [Test]
        public void Resolve_Matches_Assigned_Clip()
        {
            AudioClip fake = AudioClip.Create("test_runstart", 1, 1, 44100, false);
            try
            {
                _library.runStart = fake;
                Assert.That(_library.Resolve(AudioEventId.RunStart), Is.SameAs(fake));
            }
            finally
            {
                _library.runStart = null;
                Object.DestroyImmediate(fake);
            }
        }
    }
}
