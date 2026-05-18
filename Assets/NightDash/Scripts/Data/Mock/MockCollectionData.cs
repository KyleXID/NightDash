// Mock collection / codex data — hand-authored arrays of (id, displayName,
// isUnlocked) tuples per category. Lives under Scripts/Data/Mock so it's
// easy to find and delete once the real metaprogression system ships.

using System.Collections.Generic;

namespace NightDash.Data.Mock
{
    public static class MockCollectionData
    {
        public readonly struct Entry
        {
            public readonly string Id;
            public readonly string DisplayName;
            public readonly bool Unlocked;
            public Entry(string id, string name, bool unlocked)
            {
                Id = id;
                DisplayName = name;
                Unlocked = unlocked;
            }
        }

        // 7 classes — matches the GDD's 7-class roster (Warrior is the
        // tutorial unlock, the rest sit behind clear conditions).
        public static readonly IReadOnlyList<Entry> Classes = new[]
        {
            // Order + ids mirror the actual ClassData SOs on disk so the
            // Collection screen can resolve each class through DataRegistry.
            new Entry("class_warrior",    "Warrior",    true),
            new Entry("class_priest",     "Priest",     true),
            new Entry("class_mage",       "Mage",       true),
            new Entry("class_archer",     "Archer",     false),
            new Entry("class_astrologer", "Astrologer", false),
            new Entry("class_gunslinger", "Gunslinger", false),
            new Entry("class_paladin",    "Paladin",    false),
        };

        // 6 stages — the Stage 1 tutorial map is always available; later
        // stages gate behind the previous stage's clear flag.
        public static readonly IReadOnlyList<Entry> Stages = new[]
        {
            new Entry("stage_01", "Forsaken Village",  true),
            new Entry("stage_02", "Twilight Hollow",   true),
            new Entry("stage_03", "Ashen Cathedral",   false),
            new Entry("stage_04", "Drowning Citadel",  false),
            new Entry("stage_05", "The Abyssal Mire",  false),
            new Entry("stage_06", "Throne of Embers",  false),
        };

        // 12 relics — meta-progression rewards. Three are unlocked by
        // default so the screen never reads as all-empty for new players.
        public static readonly IReadOnlyList<Entry> Relics = new[]
        {
            new Entry("relic_pendant",  "Bone Pendant",   true),
            new Entry("relic_chalice",  "Empty Chalice",  true),
            new Entry("relic_charm",    "Wax Charm",      true),
            new Entry("relic_dagger",   "Twin Dagger",    false),
            new Entry("relic_lantern",  "Soul Lantern",   false),
            new Entry("relic_locket",   "Mourning Locket",false),
            new Entry("relic_mirror",   "Cracked Mirror", false),
            new Entry("relic_quill",    "Black Quill",    false),
            new Entry("relic_ring",     "Iron Ring",      false),
            new Entry("relic_sigil",    "Faded Sigil",    false),
            new Entry("relic_thurible", "Silver Thurible",false),
            new Entry("relic_veil",     "Widow's Veil",   false),
        };
    }
}
