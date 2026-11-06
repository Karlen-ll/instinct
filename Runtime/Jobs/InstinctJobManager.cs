using System;
using System.Collections.Generic;
using Instinct.Core.Goap;
using Instinct.Core.Math;
using Instinct.Core.Memory;
using Instinct.Core.State;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Instinct.Runtime.Jobs
{
    [DefaultExecutionOrder(50)]
    public sealed class InstinctJobManager : MonoBehaviour
    {
        public static InstinctJobManager Instance { get; private set; } = null!;

        private const int MaxBatchSize = 128;
        private const int MaxPlanDepth = 8;
        private const int StatCount = (int)StatKey.Count;

        private readonly List<InstinctAgent> _pendingAgents = new(MaxBatchSize * 2);

        private NativeArray<InstinctGoal> _sharedGoals;
        private NativeArray<InstinctAction> _sharedActions;
        private NativeArray<Consideration> _sharedConsiderations;

        private NativeArray<AgentJobInput> _agentInputs;
        private NativeArray<float> _flattenedStats;
        private NativeArray<PlanResult> _results;
        private NativeArray<ActionId> _flattenedOutputPlans;

        private JobHandle _activeJobHandle;
        private int _scheduledCount;

        private void Awake()
        {
            Instance = this;

            _agentInputs = new NativeArray<AgentJobInput>(MaxBatchSize, Allocator.Persistent);
            _flattenedStats = new NativeArray<float>(MaxBatchSize * StatCount, Allocator.Persistent);
            _results = new NativeArray<PlanResult>(MaxBatchSize, Allocator.Persistent);
            _flattenedOutputPlans = new NativeArray<ActionId>(MaxBatchSize * MaxPlanDepth, Allocator.Persistent);
        }

        public void InitializeSharedDatabase(
            ReadOnlySpan<InstinctGoal> goals,
            ReadOnlySpan<InstinctAction> actions,
            ReadOnlySpan<Consideration> considerations)
        {
            _activeJobHandle.Complete();

            if (_sharedGoals.IsCreated) _sharedGoals.Dispose();
            if (_sharedActions.IsCreated) _sharedActions.Dispose();
            if (_sharedConsiderations.IsCreated) _sharedConsiderations.Dispose();

            _sharedGoals = new NativeArray<InstinctGoal>(goals.Length, Allocator.Persistent);
            _sharedActions = new NativeArray<InstinctAction>(actions.Length, Allocator.Persistent);
            _sharedConsiderations = new NativeArray<Consideration>(considerations.Length, Allocator.Persistent);

            goals.CopyTo(_sharedGoals.AsSpan());
            actions.CopyTo(_sharedActions.AsSpan());
            considerations.CopyTo(_sharedConsiderations.AsSpan());
        }

        public void EnqueueAgent(InstinctAgent agent)
        {
            _pendingAgents.Add(agent);
        }

        private void Update()
        {
            if (_pendingAgents.Count == 0 || !_sharedGoals.IsCreated)
                return;

            // Берем не более MaxBatchSize агентов за один кадр; остальные перейдут на следующий кадр
            _scheduledCount = Math.Min(_pendingAgents.Count, MaxBatchSize);
            Span<float> statsDestination = _flattenedStats.AsSpan();

            for (int i = 0; i < _scheduledCount; i++)
            {
                InstinctAgent agent = _pendingAgents[i];
                agent.PrepareSensorsAndSync();

                _agentInputs[i] = new AgentJobInput
                {
                    StartWorldState = agent.Blackboard.LocalWorldState,
                    CurrentGoal = agent.CurrentGoal,
                    MaxDepth = MaxPlanDepth
                };

                Span<float> slot = statsDestination.Slice(i * StatCount, StatCount);
                agent.Blackboard.AllStats.CopyTo(slot);
            }

            var job = new GoapPlanningJob
            {
                SharedGoals = _sharedGoals,
                SharedActions = _sharedActions,
                SharedConsiderations = _sharedConsiderations,
                AgentInputs = _agentInputs,
                FlattenedStats = _flattenedStats,
                StatStride = StatCount,
                Results = _results,
                FlattenedOutputPlans = _flattenedOutputPlans,
                PlanStride = MaxPlanDepth
            };

            _activeJobHandle = job.Schedule(_scheduledCount, innerloopBatchCount: 4);
            JobHandle.ScheduleBatchedJobs();
        }

        private void LateUpdate()
        {
            if (_scheduledCount == 0) return;

            _activeJobHandle.Complete();

            ReadOnlySpan<ActionId> allPlans = _flattenedOutputPlans.AsReadOnlySpan();

            for (int i = 0; i < _scheduledCount; i++)
            {
                InstinctAgent agent = _pendingAgents[i];
                if (agent != null && agent.isActiveAndEnabled)
                {
                    PlanResult result = _results[i];
                    ReadOnlySpan<ActionId> planSlice = allPlans.Slice(i * MaxPlanDepth, result.PlanLength);
                    agent.ApplyPlanResult(result, planSlice);
                }
            }

            // Удаляем только обработанный батч, сохраняя хвост очереди на следующий кадр
            _pendingAgents.RemoveRange(0, _scheduledCount);
            _scheduledCount = 0;
        }

        private void OnDestroy()
        {
            _activeJobHandle.Complete();

            if (_sharedGoals.IsCreated) _sharedGoals.Dispose();
            if (_sharedActions.IsCreated) _sharedActions.Dispose();
            if (_sharedConsiderations.IsCreated) _sharedConsiderations.Dispose();

            if (_agentInputs.IsCreated) _agentInputs.Dispose();
            if (_flattenedStats.IsCreated) _flattenedStats.Dispose();
            if (_results.IsCreated) _results.Dispose();
            if (_flattenedOutputPlans.IsCreated) _flattenedOutputPlans.Dispose();
        }
    }
}