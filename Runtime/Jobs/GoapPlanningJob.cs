using System;
using Instinct.Core.Goap;
using Instinct.Core.Math;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Instinct.Runtime.Jobs
{
    /// <summary>
    /// Входной снимок состояния конкретного агента для батча.
    /// </summary>
    public struct AgentJobInput
    {
        public ulong StartWorldState;
        public byte MaxDepth;
    }

    // TIP: FloatMode.Fast разрешает Burst заменять скалярные операции на SIMD-инструкции (AVX/NEON)
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast, CompileSynchronously = true)]
    public struct GoapPlanningJob : IJobParallelFor
    {
        private const int MaxNodes = 64;
        private const int MaxActions = 32;

        // --- Общая база знаний (Read-Only для всех потоков) ---
        [ReadOnly] public NativeArray<InstinctGoal> SharedGoals;
        [ReadOnly] public NativeArray<InstinctAction> SharedActions;
        [ReadOnly] public NativeArray<Consideration> SharedConsiderations;

        // --- Входные данные батча агентов ---
        [ReadOnly] public NativeArray<AgentJobInput> AgentInputs;

        // Плоский массив всех параметров Blackboard: размер = AgentCount * StatStride
        [ReadOnly] public NativeArray<float> FlattenedStats;
        public int StatStride;

        // --- Выходные буферы ---
        [WriteOnly] public NativeArray<PlanResult> Results;

        // Плоский массив готовых планов: размер = AgentCount * PlanStride
        [NativeDisableParallelForRestriction]
        [WriteOnly] public NativeArray<ActionId> FlattenedOutputPlans;
        public int PlanStride;

        public void Execute(int index)
        {
            AgentJobInput input = AgentInputs[index];

            // 1. Вырезаем срезы памяти конкретного агента по его индексу в батче
            ReadOnlySpan<float> agentStats = FlattenedStats
                .GetSubArray(index * StatStride, StatStride)
                .AsReadOnlySpan();

            Span<ActionId> agentPlanBuffer = FlattenedOutputPlans
                .GetSubArray(index * PlanStride, PlanStride)
                .AsSpan();

            // 2. Выделяем временную память под граф A* прямо на стеке рабочего потока (~2.3 КБ)
            // TIP: stackalloc внутри Execute полностью избавляет от необходимости держать пул NativeArray под узлы A*
            Span<PlanNode> nodePool = stackalloc PlanNode[MaxNodes];
            Span<ushort> openSet = stackalloc ushort[MaxNodes];
            Span<ulong> closedStates = stackalloc ulong[MaxNodes];
            Span<float> actionCosts = stackalloc float[MaxActions];

            var workspace = new PlannerWorkspace(nodePool, openSet, closedStates, actionCosts);

            // 3. Формируем запрос и запускаем планировщик
            var request = new PlanRequest(
                startState: input.StartWorldState,
                goalState: default,
                actions: SharedActions.AsReadOnlySpan(),
                considerationPool: SharedConsiderations.AsReadOnlySpan(),
                blackboardStats: agentStats,
                maxDepth: input.MaxDepth
            );

            PlanResult result = GoapPlanner.SelectGoalAndPlan(
                SharedGoals.AsReadOnlySpan(),
                in request,
                ref workspace,
                agentPlanBuffer
            );

            Results[index] = result;
        }
    }
}