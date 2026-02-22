using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightDash.Data
{
    public enum WeaponType
    {
        Melee,
        Projectile,
        Aoe,
        Summon,
    }

    public enum PassiveCategory
    {
        Stat,
        Mechanic,
        Risk,
        EvolutionKey,
    }

    public enum StatOperation
    {
        Flat,
        PercentAdd,
        PercentMul,
    }

    public enum PassiveConditionType
    {
        Always,
        LowHp,
        OnKill,
        OnHit,
        Timed,
    }

    public enum DifficultyCategory
    {
        Combat,
        Survival,
        Mechanic,
    }

    [Serializable]
    public struct WeaponLevelCurve
    {
        public int level;
        public float powerCoeff;
        public float cooldown;
        public float range;
    }

    [Serializable]
    public struct WeaponSpecialFlags
    {
        public bool canPierce;
        public bool canCrit;
        public bool hasKnockback;
    }

    [Serializable]
    public struct PassiveEffect
    {
        public string stat;
        public StatOperation op;
        public float value;
    }

    [Serializable]
    public struct PassiveCondition
    {
        public PassiveConditionType type;
        public string arg;
    }

    [Serializable]
    public struct SpawnEntry
    {
        public string enemyId;
        public int weight;
        public int spawnPerMin;
    }

    [Serializable]
    public struct SpawnPhase
    {
        public int fromSec;
        public int toSec;
        public List<SpawnEntry> entries;
    }

    [Serializable]
    public struct EliteEvent
    {
        public int atSec;
        public string enemyId;
        public int count;
    }

    [Serializable]
    public struct EnemyModifierValues
    {
        public float hpPct;
        public float moveSpeedPct;
        public float spawnRatePct;
    }

    [Serializable]
    public struct PlayerModifierValues
    {
        public float healRatePct;
        public float cooldownPct;
    }

    [Serializable]
    public struct RuntimeEffectValues
    {
        public float hazardMultiplier;
        public bool onKillExplosion;
    }

    [Serializable]
    public struct MetaNodeEffect
    {
        public string stat;
        public StatOperation op;
        public float value;
    }

    [Serializable]
    public struct MetaNodeData
    {
        public string nodeId;
        public string name;
        public int cost;
        public List<string> prereqNodeIds;
        public List<MetaNodeEffect> effects;
    }
}
