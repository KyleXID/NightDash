using System.Collections.Generic;
using NightDash.Data;

namespace NightDash.Runtime
{
    internal static class RunSelectionLobbyOptions
    {
        public static void AddStageIds(List<StageData> stages, List<string> target)
        {
            if (stages == null)
            {
                return;
            }

            for (int i = 0; i < stages.Count; i++)
            {
                if (stages[i] == null || string.IsNullOrWhiteSpace(stages[i].id))
                {
                    continue;
                }

                string id = stages[i].id.Trim();
                if (!target.Contains(id))
                {
                    target.Add(id);
                }
            }
        }

        public static void AddClassIds(List<ClassData> classes, List<string> target)
        {
            if (classes == null)
            {
                return;
            }

            for (int i = 0; i < classes.Count; i++)
            {
                if (classes[i] == null || string.IsNullOrWhiteSpace(classes[i].id))
                {
                    continue;
                }

                string id = classes[i].id.Trim();
                if (!target.Contains(id))
                {
                    target.Add(id);
                }
            }
        }

        public static void EnsureFallbackIds(List<string> target, string fallback)
        {
            if (target.Count > 0)
            {
                return;
            }

            target.Add(fallback);
        }

        public static int FindIndexOrDefault(List<string> values, string current)
        {
            if (values.Count == 0)
            {
                return 0;
            }

            int idx = values.IndexOf(current);
            return idx >= 0 ? idx : 0;
        }

        public static string SafeGet(List<string> values, int index, string fallback)
        {
            if (values.Count == 0)
            {
                return fallback;
            }

            if (index < 0 || index >= values.Count)
            {
                return values[0];
            }

            return values[index];
        }
    }
}
