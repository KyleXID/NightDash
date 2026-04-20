using System.Collections.Generic;
using UnityEngine;

namespace NightDash.Data
{
    /// <summary>
    /// GDD(ops/02_localization_policy.md) ko-KR/en-US MVP 저장소.
    /// 각 엔트리는 `{domain}.{subdomain}.{id}` 키 + 언어별 문자열을 갖는다.
    /// 문체·길이 규칙은 기획 소스(strings_master.csv)에서 관리, 이 SO는 런타임 번역 표.
    /// </summary>
    [CreateAssetMenu(menuName = "NightDash/Data/Localization Table", fileName = "localization_table")]
    public sealed class LocalizationTable : ScriptableObject
    {
        public List<LocalizedString> entries = new();
    }

    [System.Serializable]
    public struct LocalizedString
    {
        public string key;
        [TextArea] public string ko;
        [TextArea] public string en;
    }

    public enum Locale
    {
        KoKR,
        EnUS,
    }
}
