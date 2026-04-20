// S3-07: SaveDataHelper 무결성 테스트.
// 우발적 손상 탐지(checksum·version·range fallback) 검증.

using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using NightDash.ECS.Systems;

namespace NightDash.Tests.EditMode
{
    [TestFixture]
    public class SaveDataHelperTests
    {
        [SetUp]
        [TearDown]
        public void ClearPlayerPrefs()
        {
            PlayerPrefs.DeleteKey(SaveDataHelper.ConquestPointsKey);
            PlayerPrefs.DeleteKey(SaveDataHelper.ConquestChecksumKey);
            PlayerPrefs.DeleteKey(SaveDataHelper.SaveVersionKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void Load_FirstRun_Returns_Default_And_True()
        {
            bool ok = SaveDataHelper.TryLoad(out int points);

            Assert.That(ok, Is.True, "처음 실행은 fallback이 아니라 기본값 초기화로 성공 반환");
            Assert.That(points, Is.EqualTo(SaveDataHelper.DefaultConquestPoints));
        }

        [Test]
        public void SaveAndLoad_RoundTrip_Preserves_Value()
        {
            SaveDataHelper.Save(42);

            bool ok = SaveDataHelper.TryLoad(out int points);

            Assert.That(ok, Is.True);
            Assert.That(points, Is.EqualTo(42));
        }

        [Test]
        public void Load_CorruptedChecksum_Falls_Back_To_Default()
        {
            SaveDataHelper.Save(42);
            PlayerPrefs.SetInt(SaveDataHelper.ConquestChecksumKey, 99999);
            PlayerPrefs.Save();

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("checksum mismatch"));
            bool ok = SaveDataHelper.TryLoad(out int points);

            Assert.That(ok, Is.False);
            Assert.That(points, Is.EqualTo(SaveDataHelper.DefaultConquestPoints));
        }

        [Test]
        public void Load_VersionMismatch_Falls_Back_To_Default()
        {
            SaveDataHelper.Save(42);
            PlayerPrefs.SetInt(SaveDataHelper.SaveVersionKey, SaveDataHelper.SaveVersion + 99);
            PlayerPrefs.Save();

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("version mismatch"));
            bool ok = SaveDataHelper.TryLoad(out int points);

            Assert.That(ok, Is.False);
            Assert.That(points, Is.EqualTo(SaveDataHelper.DefaultConquestPoints));
        }

        [Test]
        public void Load_NegativeValue_Falls_Back_To_Default()
        {
            int negative = -1;
            int checksum = SaveDataHelper.ComputeChecksum(negative, SaveDataHelper.SaveVersion);
            PlayerPrefs.SetInt(SaveDataHelper.ConquestPointsKey, negative);
            PlayerPrefs.SetInt(SaveDataHelper.ConquestChecksumKey, checksum);
            PlayerPrefs.SetInt(SaveDataHelper.SaveVersionKey, SaveDataHelper.SaveVersion);
            PlayerPrefs.Save();

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("out of range"));
            bool ok = SaveDataHelper.TryLoad(out int points);

            Assert.That(ok, Is.False);
            Assert.That(points, Is.EqualTo(SaveDataHelper.DefaultConquestPoints));
        }

        [Test]
        public void Save_OverMax_Clamps_To_Max()
        {
            SaveDataHelper.Save(SaveDataHelper.MaxConquestPoints + 500);

            bool ok = SaveDataHelper.TryLoad(out int points);

            Assert.That(ok, Is.True);
            Assert.That(points, Is.EqualTo(SaveDataHelper.MaxConquestPoints));
        }

        [Test]
        public void Save_NegativeValue_Clamps_To_Zero()
        {
            SaveDataHelper.Save(-50);

            bool ok = SaveDataHelper.TryLoad(out int points);

            Assert.That(ok, Is.True);
            Assert.That(points, Is.EqualTo(0));
        }

        [Test]
        public void Checksum_Is_Deterministic()
        {
            int a = SaveDataHelper.ComputeChecksum(42, SaveDataHelper.SaveVersion);
            int b = SaveDataHelper.ComputeChecksum(42, SaveDataHelper.SaveVersion);

            Assert.That(a, Is.EqualTo(b));
        }

        [Test]
        public void Checksum_Differs_For_Different_Values()
        {
            int a = SaveDataHelper.ComputeChecksum(42, SaveDataHelper.SaveVersion);
            int b = SaveDataHelper.ComputeChecksum(43, SaveDataHelper.SaveVersion);

            Assert.That(a, Is.Not.EqualTo(b));
        }
    }
}
