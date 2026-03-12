using NightDash.ECS.Components;
using Unity.Entities;

namespace NightDash.ECS.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(SaveSystem))]
    public partial struct RunNavigationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RunNavigationRequest>();
            state.RequireForUpdate<GameLoopState>();
            state.RequireForUpdate<DataLoadState>();
            state.RequireForUpdate<ResultSnapshot>();
        }

        public void OnUpdate(ref SystemState state)
        {
            RefRW<RunNavigationRequest> navigation = SystemAPI.GetSingletonRW<RunNavigationRequest>();
            RefRW<DataLoadState> load = SystemAPI.GetSingletonRW<DataLoadState>();
            RefRW<GameLoopState> loop = SystemAPI.GetSingletonRW<GameLoopState>();
            RefRW<ResultSnapshot> snapshot = SystemAPI.GetSingletonRW<ResultSnapshot>();
            if (navigation.ValueRO.IsPending == 0)
            {
                return;
            }

            switch (navigation.ValueRO.Action)
            {
                case RunNavigationAction.Retry:
                    ApplyRetry(ref state, ref load, ref loop, ref snapshot);
                    break;
                case RunNavigationAction.ReturnToLobby:
                    ApplyReturnToLobby(ref state, ref navigation, ref loop, ref snapshot);
                    break;
                default:
                    navigation.ValueRW.Action = RunNavigationAction.None;
                    navigation.ValueRW.IsPending = 0;
                    break;
            }
        }

        private static void ApplyRetry(
            ref SystemState state,
            ref RefRW<DataLoadState> load,
            ref RefRW<GameLoopState> loop,
            ref RefRW<ResultSnapshot> snapshot)
        {
            load.ValueRW.HasLoaded = 0;

            loop.ValueRW.IsRunActive = 0;
            loop.ValueRW.Status = RunStatus.Loading;
            loop.ValueRW.PendingLevelUps = 0;
            loop.ValueRW.ElapsedTime = 0f;

            ResetPresentationState(ref state, ref snapshot);
        }

        private static void ApplyReturnToLobby(
            ref SystemState state,
            ref RefRW<RunNavigationRequest> navigation,
            ref RefRW<GameLoopState> loop,
            ref RefRW<ResultSnapshot> snapshot)
        {
            loop.ValueRW.IsRunActive = 0;
            loop.ValueRW.Status = RunStatus.Loading;
            loop.ValueRW.PendingLevelUps = 0;

            navigation.ValueRW.Action = RunNavigationAction.None;
            navigation.ValueRW.IsPending = 0;
            ResetPresentationState(ref state, ref snapshot);
        }

        private static void ResetPresentationState(
            ref SystemState state,
            ref RefRW<ResultSnapshot> snapshot)
        {
            snapshot.ValueRW.HasSnapshot = 0;
            snapshot.ValueRW.IsVictory = 0;
            snapshot.ValueRW.ElapsedTime = 0f;
            snapshot.ValueRW.FinalLevel = 1;
            snapshot.ValueRW.KillCount = 0;
            snapshot.ValueRW.GoldEarned = 0;
            snapshot.ValueRW.SoulsEarned = 0;
            snapshot.ValueRW.RewardGranted = 0;

            EntityManager entityManager = state.EntityManager;
            EntityQuery rewardQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<BossRewardState>());
            if (!rewardQuery.IsEmptyIgnoreFilter)
            {
                Entity rewardEntity = rewardQuery.GetSingletonEntity();
                BossRewardState reward = entityManager.GetComponentData<BossRewardState>(rewardEntity);
                reward.HasPendingReward = 0;
                reward.EvolutionResolved = 0;
                entityManager.SetComponentData(rewardEntity, reward);
            }

            EntityQuery confirmQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<BossRewardConfirmRequest>());
            if (!confirmQuery.IsEmptyIgnoreFilter)
            {
                Entity confirmEntity = confirmQuery.GetSingletonEntity();
                BossRewardConfirmRequest confirm = entityManager.GetComponentData<BossRewardConfirmRequest>(confirmEntity);
                confirm.IsPending = 0;
                entityManager.SetComponentData(confirmEntity, confirm);
            }
        }
    }
}
