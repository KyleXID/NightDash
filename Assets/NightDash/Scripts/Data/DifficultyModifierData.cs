using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace NightDash.Data
{
    [CreateAssetMenu(menuName = "NightDash/Data/Difficulty Modifier Data", fileName = "difficulty_")]
    public sealed class DifficultyModifierData : ScriptableObject
    {
        [Header("Identity")]
        [FormerlySerializedAs("modifierId")] public string id;
        [FormerlySerializedAs("name")] public string displayName;
        public Sprite icon;

        [Header("Classification")]
        public DifficultyCategory category = DifficultyCategory.Combat;

        // Stack-aware definition. Each entry is a discrete level (Lv.1 = stages[0])
        // with its own risk + value scaling. When stages is empty the SO falls back
        // to the legacy single-value fields below — that path keeps pre-stacking
        // assets working without an Inspector edit; new assets should populate
        // stages directly and leave the legacy block at its default.
        [Header("Stages (Lv.1 .. Lv.N)")]
        public DifficultyStage[] stages;

        // ---- Legacy single-value fields ------------------------------------
        // Kept for backwards compatibility. Treated as the source for Lv.1
        // whenever `stages` is empty. New work should drive stages[] instead.
        [Header("Legacy (used only when Stages is empty)")]
        [FormerlySerializedAs("riskScore")] public int riskPoint = 1;
        public EnemyModifierValues enemyModifiers;
        public PlayerModifierValues playerModifiers;
        public RuntimeEffectValues runtimeEffects;
        [FormerlySerializedAs("rewardMultiplierBonus")] public float rewardBonusPct = 0.1f;

        // Legacy assets without an explicit `stages[]` synthesize three levels
        // at runtime by scaling the single-value block. The migration helper
        // can still bake the same values into the asset for hand-tuning, but
        // the default lets the system feel stackable even before migration.
        private const int LegacyStageCount = 3;
        private static readonly float[] LegacyValueScale = { 1.0f, 1.5f, 2.0f };
        private static readonly float[] LegacyRiskScale  = { 1.0f, 1.5f, 2.0f };

        // Number of selectable levels — derived from `stages[]` when present
        // or the legacy default (3) when the asset hasn't been migrated yet.
        public int MaxLevel => (stages != null && stages.Length > 0)
            ? stages.Length
            : LegacyStageCount;

        // True when the asset still relies on the single-value legacy block.
        // Editor migration helper flips this to false by populating stages.
        public bool IsLegacy => stages == null || stages.Length == 0;

        // Resolve a 1-based level into the flattened tuple consumed by ECS
        // (RunSelectionOverrideSystem / RunSelectionLobbyWorldBridge). Returns
        // false for level <= 0 (modifier inactive) or out-of-range levels.
        public bool TryGetStage(int level, out DifficultyStage resolved)
        {
            resolved = default;
            if (level <= 0) return false;

            if (stages != null && stages.Length > 0)
            {
                if (level > stages.Length) return false;
                resolved = stages[level - 1];
                return true;
            }

            // Legacy fallback: synthesize Lv.1..Lv.LegacyStageCount by scaling
            // the single-value block. Designers who want bespoke curves run
            // the migration helper and tune `stages[]` directly.
            if (level > LegacyStageCount) return false;
            float vs = LegacyValueScale[level - 1];
            float rs = LegacyRiskScale[level - 1];

            // Boolean-only modifiers (e.g. onKillExplosion alone) would scale
            // to nothing at higher levels — every numeric stat is 0, so
            // multiplying by 1.0/1.5/2.0 still yields 0. Synthesize a hazard
            // bump per stage so stacking still produces a visible effect and
            // the chip description has something to show.
            bool isBooleanOnly = runtimeEffects.onKillExplosion
                && Mathf.Abs(runtimeEffects.hazardMultiplier) < 0.0001f
                && Mathf.Abs(enemyModifiers.hpPct) < 0.0001f
                && Mathf.Abs(enemyModifiers.moveSpeedPct) < 0.0001f
                && Mathf.Abs(enemyModifiers.spawnRatePct) < 0.0001f
                && Mathf.Abs(playerModifiers.healRatePct) < 0.0001f
                && Mathf.Abs(playerModifiers.cooldownPct) < 0.0001f;
            // Lv.1 = 0, Lv.2 = 0.5, Lv.3 = 1.0 → reads as "위험 +50% / +100%".
            float synthHazard = isBooleanOnly ? (level - 1) * 0.5f : 0f;

            resolved = new DifficultyStage
            {
                label = $"Lv.{level}",
                riskPoint = Mathf.Max(1, Mathf.RoundToInt(riskPoint * rs)),
                enemyModifiers = new EnemyModifierValues
                {
                    hpPct = enemyModifiers.hpPct * vs,
                    moveSpeedPct = enemyModifiers.moveSpeedPct * vs,
                    spawnRatePct = enemyModifiers.spawnRatePct * vs,
                },
                playerModifiers = new PlayerModifierValues
                {
                    healRatePct = playerModifiers.healRatePct * vs,
                    cooldownPct = playerModifiers.cooldownPct * vs,
                },
                runtimeEffects = new RuntimeEffectValues
                {
                    hazardMultiplier = runtimeEffects.hazardMultiplier * vs + synthHazard,
                    onKillExplosion = runtimeEffects.onKillExplosion,
                },
                rewardBonusPct = rewardBonusPct * vs,
            };
            return true;
        }
    }

    [Serializable]
    public struct DifficultyStage
    {
        // Short label rendered in UI chips (e.g. "Lv.2"). Optional — empty
        // strings fall back to $"Lv.{index+1}" at display time.
        public string label;
        public int riskPoint;
        public EnemyModifierValues enemyModifiers;
        public PlayerModifierValues playerModifiers;
        public RuntimeEffectValues runtimeEffects;
        public float rewardBonusPct;
    }
}
