using System;
using Instinct.Core.Goap;
using Instinct.Core.Memory;
using Instinct.Runtime.Execution;
using Instinct.Runtime.Jobs;
using Instinct.Runtime.Sensors;
using UnityEngine;

namespace Instinct.Runtime
{
    public sealed class InstinctAgent : MonoBehaviour
    {
        private const byte MaxPlanDepth = 8;
        private const int MaxActionIdCapacity = 16;

        [Header("Планирование")]
        [SerializeField] private float _replanInterval = 0.25f;
        [SerializeField] private ulong _sharedSquadFlagsMask;

        private readonly AgentBlackboard _blackboard = new();
        private SquadBlackboard? _squadBlackboard;
        private ISensor[] _sensors = Array.Empty<ISensor>();

        private readonly IActionExecutor?[] _executorLookup = new IActionExecutor?[MaxActionIdCapacity];

        private readonly ActionId[] _activePlan = new ActionId[MaxPlanDepth];

        private byte _activePlanLength;
        private byte _currentStepIndex;
        private GoalId _currentGoalId;
        private IActionExecutor? _currentExecutor;
        private float _replanTimer;
        private bool _isWaitingForPlan;

        public AgentBlackboard Blackboard => _blackboard;
        public GoalId CurrentGoal => _currentGoalId;

        public void Initialize(SquadBlackboard? squad = null)
        {
            _squadBlackboard = squad;
            _sensors = GetComponentsInChildren<ISensor>();

            IActionExecutor[] executors = GetComponentsInChildren<IActionExecutor>();
            for (int i = 0; i < executors.Length; i++)
            {
                int id = (int)executors[i].ActionId;
                _executorLookup[id] = executors[i];
            }

            // TIP: Случайное смещение таймера распределяет пики нагрузки CPU по кадрам.
            _replanTimer = UnityEngine.Random.Range(0f, _replanInterval);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _replanTimer -= dt;

            if ((_replanTimer <= 0f || _currentExecutor == null) && !_isWaitingForPlan)
            {
                if (InstinctJobManager.Instance != null)
                {
                    _replanTimer = _replanInterval;
                    _isWaitingForPlan = true;
                    InstinctJobManager.Instance.EnqueueAgent(this);
                }
                // TIP: Если менеджер ещё не инициализирован — не защёлкиваем флаг, попробуем позже.
            }

            TickActivePlan(dt);
        }

        /// <summary>Вызывается менеджером в главном потоке перед упаковкой данных в NativeArray.</summary>
        public void PrepareSensorsAndSync()
        {
            if (_squadBlackboard != null)
                _blackboard.SyncFromSquad(_squadBlackboard, _sharedSquadFlagsMask);

            for (int i = 0; i < _sensors.Length; i++)
                _sensors[i].CollectData(_blackboard);
        }

        /// <summary>Вызывается менеджером в LateUpdate после завершения GoapPlanningJob.</summary>
        public void ApplyPlanResult(PlanResult result, ReadOnlySpan<ActionId> newPlan)
        {
            _isWaitingForPlan = false;

            if (result.Status != PlanStatus.Success || result.PlanLength == 0)
            {
                AbortCurrentPlan();
                return;
            }

            ActionId newFirstAction = newPlan[0];
            bool isSameCurrentAction =
                _currentExecutor != null && _currentExecutor.ActionId == newFirstAction;

            newPlan.CopyTo(_activePlan);
            _activePlanLength = result.PlanLength;
            _currentGoalId = result.ActiveGoal;
            _currentStepIndex = 0;

            // Бесшовное обновление: не дёргаем OnEnter/OnExit у того же действия.
            if (!isSameCurrentAction)
                StartAction(newFirstAction);
        }

        private void TickActivePlan(float deltaTime)
        {
            if (_currentExecutor == null) return;

            ExecutionStatus status = _currentExecutor.OnTick(_blackboard, deltaTime);

            switch (status)
            {
                case ExecutionStatus.Running:
                    break;

                case ExecutionStatus.Success:
                    _currentExecutor.OnExit(_blackboard, wasInterrupted: false);
                    _currentExecutor = null;
                    _currentStepIndex++;

                    if (_currentStepIndex < _activePlanLength)
                        StartAction(_activePlan[_currentStepIndex]);
                    else
                        _replanTimer = 0f;
                    break;

                case ExecutionStatus.Failed:
                    AbortCurrentPlan();
                    _replanTimer = 0f;
                    break;
            }
        }

        private void StartAction(ActionId actionId)
        {
            _currentExecutor?.OnExit(_blackboard, wasInterrupted: true);

            IActionExecutor? nextExecutor = _executorLookup[(int)actionId];
            if (nextExecutor == null)
            {
                AbortCurrentPlan();
                return;
            }

            _currentExecutor = nextExecutor;
            _currentExecutor.OnEnter(_blackboard);
        }

        private void AbortCurrentPlan()
        {
            if (_currentExecutor != null)
            {
                _currentExecutor.OnExit(_blackboard, wasInterrupted: true);
                _currentExecutor = null;
            }

            _activePlanLength = 0;
            _currentStepIndex = 0;
            _currentGoalId = GoalId.None;
        }
    }
}