using System;
using UnityEngine;

namespace NightDash.ECS.Systems
{
    /// <summary>
    /// PlayerPrefs 기반 세이브 IO 유틸. 체크섬·버전 태그·범위 검증으로
    /// 우발적 손상(부분 쓰기, 수동 편집 오타 등)에 대한 최소 방어선을 제공한다.
    /// 암호학적 안전성은 목표가 아님 (오프라인 싱글플레이어).
    /// </summary>
    internal static class SaveDataHelper
    {
        internal const string ConquestPointsKey = "NightDash_ConquestPoints";
        internal const string ConquestChecksumKey = "NightDash_ConquestPoints_chk";
        internal const string SaveVersionKey = "NightDash_SaveVersion";
        internal const int SaveVersion = 1;
        internal const int DefaultConquestPoints = 0;
        internal const int MaxConquestPoints = 1_000_000_000;

        internal static int ComputeChecksum(int value, int version)
        {
            // XOR + version mixing. 충돌 가능성은 있으나 우발 손상 탐지엔 충분.
            unchecked
            {
                return value ^ (int)(version * 0x5F3759DF);
            }
        }

        internal static bool TryLoad(out int conquestPoints)
        {
            int storedVersion = PlayerPrefs.GetInt(SaveVersionKey, 0);
            int storedPoints = PlayerPrefs.GetInt(ConquestPointsKey, DefaultConquestPoints);
            int storedChecksum = PlayerPrefs.GetInt(ConquestChecksumKey, 0);

            if (storedVersion == 0)
            {
                // 최초 실행: 기록 없음 → 기본값으로 초기화.
                conquestPoints = DefaultConquestPoints;
                return true;
            }

            if (storedVersion != SaveVersion)
            {
                Debug.LogWarning(
                    $"[SaveSystem] Save version mismatch (stored={storedVersion}, expected={SaveVersion}). Falling back to default.");
                conquestPoints = DefaultConquestPoints;
                return false;
            }

            int expectedChecksum = ComputeChecksum(storedPoints, storedVersion);
            if (expectedChecksum != storedChecksum)
            {
                Debug.LogWarning("[SaveSystem] Save data checksum mismatch. Falling back to default.");
                conquestPoints = DefaultConquestPoints;
                return false;
            }

            if (storedPoints < 0 || storedPoints > MaxConquestPoints)
            {
                Debug.LogWarning(
                    $"[SaveSystem] Conquest points out of range ({storedPoints}). Falling back to default.");
                conquestPoints = DefaultConquestPoints;
                return false;
            }

            conquestPoints = storedPoints;
            return true;
        }

        internal static void Save(int conquestPoints)
        {
            int clamped = Mathf.Clamp(conquestPoints, 0, MaxConquestPoints);
            int checksum = ComputeChecksum(clamped, SaveVersion);

            try
            {
                PlayerPrefs.SetInt(ConquestPointsKey, clamped);
                PlayerPrefs.SetInt(ConquestChecksumKey, checksum);
                PlayerPrefs.SetInt(SaveVersionKey, SaveVersion);
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] PlayerPrefs.Save failed: {ex.Message}");
            }
        }
    }
}
