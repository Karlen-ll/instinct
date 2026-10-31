using System;
using System.Collections.Generic;
using Instinct.Core.Goap;
using Instinct.Core.Math;
using Instinct.Core.Memory;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Instinct.Runtime.Jobs
{
    [DefaultExecutionOrder(-50)]
    public sealed class InstinctJobManager : MonoBehaviour
    {
        public static InstinctJobManager Instance { get; private set; } = null!;

        private const int MaxBatchSize = 128; // Максимум пересчетов плана за один кадр
        private const int MaxPlanDepth = 8;
        private const int StatCount = (int)StatKey.Count;

        // Очередь агентов, запросивших пересчет в текущем кадре
        private readonly List<InstinctAgent> _pendingAgents = new(MaxBatchSize);

        // Постоянные (Persistent) буферы базы знаний
        private NativeArray<InstinctGoal> _sharedGoals;
        private NativeArray<InstinctAction> _sharedActions;
        private NativeArray<Consideration> _sharedConsiderations;

        // Постоянные буферы батча
        private NativeArray<AgentJobInput> _agentInputs;
        private NativeArray<float> _flattenedStats;
        private NativeArray<PlanResult> _results;
        private NativeArray<ActionId> _flattenedOutputPlans;

        private JobHandle _activeJobHandle;
        private int _scheduledCount;

        private void Awake()
        {
            Instance = this;

            // Выделяем неуправляемую память один раз при старте сцены
            _agentInputs = new NativeArray<AgentJobInput>(MaxBatchSize, Allocator.Persistent);
            _flattenedStats = new NativeArray<float>(MaxBatchSize * StatCount, Allocator.Persistent);
            _results = new NativeArray<PlanResult>(MaxBatchSize, Allocator.Persistent);
            _flattenedOutputPlans = new NativeArray<ActionId>(MaxBatchSize * MaxPlanDepth, Allocator.Persistent);
        }

        /// <summary>
        /// Загружает общую конфигурацию целей, действий и кривых в неуправляемую память.
        /// </summary>
        public void InitializeSharedDatabase(
            ReadOnlySpan<InstinctGoal> goals,
            ReadOnlySpan<InstinctAction> actions,
            ReadOnlySpan<Consideration> considerations)
        {
            _sharedGoals = new NativeArray<InstinctGoal>(goals.Length, Allocator.Persistent);
            _sharedActions = new NativeArray<InstinctAction>(actions.Length, Allocator.Persistent);
            _sharedConsiderations = new NativeArray<Consideration>(considerations.Length, Allocator.Persistent);

            goals.CopyTo(_sharedGoals.AsSpan());
            actions.CopyTo(_sharedActions.AsSpan());
            considerations.CopyTo(_sharedConsiderations.AsSpan());
        }

        /// <summary>
        /// Вызывается из InstinctAgent, когда подошел таймер перепланирования.
        /// </summary>
        public void EnqueueAgent(InstinctAgent agent)
        {
            if (_pendingAgents.Count < MaxBatchSize)
            {
                _pendingAgents.Add(agent);
            }
        }

        private void Update()
        {
            _scheduledCount = _pendingAgents.Count;
            if (_scheduledCount == 0) return;

            Span<float> statsDestination = _flattenedStats.AsSpan();

            // 1. Собираем актуальные данные сенсоров и упаковываем Blackboard в плоский буфер
            for (int i = 0; i < _scheduledCount; i++)
            {
                InstinctAgent agent = _pendingAgents[i];
                agent.PrepareSensorsAndSync();

                _agentInputs[i] = new AgentJobInput
                {
                    StartWorldState = agent.Blackboard.LocalWorldState,
                    MaxDepth = MaxPlanDepth
                };

                // Быстрое копирование блока памяти (memmove) в нужный слот батча
                Span<float> slot = statsDestination.Slice(i * StatCount, StatCount);
                agent.Blackboard.AllStats.CopyTo(slot);
            }

            // 2. Формируем и запускаем параллельный джоб
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

            // TIP: innerloopBatchCount = 4 распределяет по 4 агента на одну задачу воркера,
            // балансируя накладные расходы планировщика потоков и загрузку ядер CPU
            _activeJobHandle = job.Schedule(_scheduledCount, innerloopBatchCount: 4);
            JobHandle.ScheduleBatchedJobs();
        }

        private void LateUpdate()
        {
            if (_scheduledCount == 0) return;

            // Дожидаемся завершения всех рабочих потоков в конце кадра
            _activeJobHandle.Complete();

            ReadOnlySpan<ActionId> allPlans = _flattenedOutputPlans.AsReadOnlySpan();

            // Рассылаем готовые планы обратно агентам
            for (int i = 0; i < _scheduledCount; i++)
            {
                PlanResult result = _results[i];
                ReadOnlySpan<ActionId> planSlice = allPlans.Slice(i * MaxPlanDepth, result.PlanLength);

                _pendingAgents[i].ApplyPlanResult(result, planSlice);
            }

            _pendingAgents.Clear();
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