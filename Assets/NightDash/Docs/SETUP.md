# NightDash Initial Setup (Unity ECS + Data)

## 1. Package check

`Packages/manifest.json` must include:

- `com.unity.entities`
- `com.unity.burst`
- `com.unity.collections`
- `com.unity.mathematics`

After opening Unity, wait for package import and script compilation.

## 2. Scene objects

In your main scene, create:

1. `NightDashBootstrap` GameObject and add `NightDashBootstrapAuthoring`
2. Player prefab/object with `PlayerAuthoring`
3. Enemy prefab with `EnemyAuthoring`
4. (Optional) Boss prefab with `EnemyAuthoring` and `isBoss = true`

Then wire `enemyPrefab` / `bossPrefab` on `NightDashBootstrapAuthoring`.

## 3. Data setup workflow

1. Run menu: `NightDash/Data/Create All Template Assets`
2. Duplicate generated `tpl_*.asset` files and rename with real IDs
3. Fill fields based on `Docs/GDD/specs/`
4. Run menu: `NightDash/Validation/Run Data Validation`
5. Resolve Console errors until 0 issues

## 4. Runtime verification checklist

Play mode should show:

- `GameLoopState.ElapsedTime` increases over time
- `DifficultyState.RiskScore` is calculated from checklist buffer
- enemies spawn periodically around the player
- stage ends at `stageDurationSeconds`
- run reward is written to `MetaProgress.ConquestPoints`

## 5. Core paths

- Data SO definitions: `Assets/NightDash/Scripts/Data`
- Data editor tools: `Assets/NightDash/Scripts/Editor`
- Data assets: `Assets/NightDash/Data`
- ECS components: `Assets/NightDash/Scripts/ECS/Components`
- ECS systems: `Assets/NightDash/Scripts/ECS/Systems`
- Authoring/Baking: `Assets/NightDash/Scripts/ECS/Authoring`
