// One-shot migration helper for the difficulty modifier stacking system.
// Seeds DifficultyModifierData.stages[] with three levels derived from the
// legacy single-value fields:
//   Lv.1 = 1.0x (verbatim copy of the legacy block)
//   Lv.2 = 1.5x scaled values + +50% risk
//   Lv.3 = 2.0x scaled values + +100% risk
// After running, the legacy fields are left intact so the runtime fallback
// remains valid; designers can tune individual stages by hand in the
// Inspector. Delete this file once all assets have been migrated and
// hand-tuned to taste.

using NightDash.Data;
using UnityEditor;
using UnityEngine;

namespace NightDash.Editor.Tools
{
    public static class DifficultyStageMigration
    {
        private const int DefaultStageCount = 3;
        private static readonly float[] ValueScales = { 1.0f, 1.5f, 2.0f };
        private static readonly float[] RiskScales  = { 1.0f, 1.5f, 2.0f };

        [MenuItem("Tools/NightDash/Migrate Difficulty Stages")]
        public static void Run()
        {
            string[] guids = AssetDatabase.FindAssets("t:DifficultyModifierData");
            if (guids == null || guids.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Difficulty Stage Migration",
                    "No DifficultyModifierData assets found.",
                    "OK");
                return;
            }

            int migrated = 0;
            int skipped = 0;
            int missing = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var data = AssetDatabase.LoadAssetAtPath<DifficultyModifierData>(path);
                if (data == null)
                {
                    // GUID resolved to a path Unity can no longer load — most
                    // often a .meta orphan whose .asset was deleted in git.
                    missing++;
                    continue;
                }

                if (data.stages != null && data.stages.Length > 0)
                {
                    skipped++;
                    continue;
                }

                data.stages = BuildStages(data);
                EditorUtility.SetDirty(data);
                migrated++;
            }

            if (migrated > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            int reachable = guids.Length - missing;
            EditorUtility.DisplayDialog(
                "Difficulty Stage Migration",
                $"Migrated: {migrated}\nSkipped (already had stages): {skipped}\n" +
                $"Reachable assets: {reachable}\nMissing (orphaned GUIDs): {missing}\n\n" +
                "Open each modifier in the Inspector to fine-tune Lv.2 / Lv.3 values.",
                "OK");
        }

        private static DifficultyStage[] BuildStages(DifficultyModifierData data)
        {
            DifficultyStage[] stages = new DifficultyStage[DefaultStageCount];
            for (int i = 0; i < DefaultStageCount; i++)
            {
                float vs = ValueScales[i];
                float rs = RiskScales[i];
                stages[i] = new DifficultyStage
                {
                    label = $"Lv.{i + 1}",
                    riskPoint = Mathf.Max(1, Mathf.RoundToInt(data.riskPoint * rs)),
                    enemyModifiers = new EnemyModifierValues
                    {
                        hpPct = data.enemyModifiers.hpPct * vs,
                        moveSpeedPct = data.enemyModifiers.moveSpeedPct * vs,
                        spawnRatePct = data.enemyModifiers.spawnRatePct * vs,
                    },
                    playerModifiers = new PlayerModifierValues
                    {
                        // Player-side debuffs scale the same way; cooldownPct is
                        // additive percent so a 2x at Lv.3 is consistent with
                        // hpPct semantics.
                        healRatePct = data.playerModifiers.healRatePct * vs,
                        cooldownPct = data.playerModifiers.cooldownPct * vs,
                    },
                    runtimeEffects = new RuntimeEffectValues
                    {
                        hazardMultiplier = data.runtimeEffects.hazardMultiplier * vs,
                        // Boolean effects don't scale — they're all-or-nothing,
                        // so we copy the legacy flag verbatim into every stage.
                        onKillExplosion = data.runtimeEffects.onKillExplosion,
                    },
                    rewardBonusPct = data.rewardBonusPct * vs,
                };
            }
            return stages;
        }
    }
}
