using System;
using System.Runtime.CompilerServices;
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

        [Header("Планирование и Инерция")]
        [SerializeField] private float _replanInterval = 0.25f;
        [SerializeField] private float _minActionCommitmentTime = 0.4f;
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
        private float _actionCommitmentTimer;
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

            _replanTimer = UnityEngine.Random.Range(0f, _replanInterval);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _replanTimer -= dt;

            if (_actionCommitmentTimer > 0f)
            {
                _actionCommitmentTimer -= dt;
            }

            if ((_replanTimer <= 0f || _currentExecutor == null)
                && !_isWaitingForPlan
                && CanInterruptCurrentAction())
            {
                _replanTimer = _replanInterval;
                _isWaitingForPlan = true;
                InstinctJobManager.Instance.EnqueueAgent(this);
            }

            TickActivePlan(dt);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool CanInterruptCurrentAction()
        {
            if (_currentExecutor == null)
                return true;

            return _currentExecutor.IsInterruptible && _actionCommitmentTimer <= 0f;
        }

        public void PrepareSensorsAndSync()
        {
            if (_squadBlackboard != null)
            {
                _blackboard.SyncFromSquad(_squadBlackboard, _sharedSquadFlagsMask);
            }

            for (int i = 0; i < _sensors.Length; i++)
            {
                _sensors[i].CollectData(_blackboard);
            }
        }

        public void ApplyPlanResult(PlanResult result, ReadOnlySpan<ActionId> newPlan)
        {
            _isWaitingForPlan = false;

            // Защита на случай, если исполнитель вошел в непрерываемую фазу пока считался Job
            if (_currentExecutor != null && !_currentExecutor.IsInterruptible)
                return;

            if (result.Status != PlanStatus.Success || result.PlanLength == 0)
            {
                AbortCurrentPlan();
                return;
            }

            ActionId newFirstAction = newPlan[0];
            bool isSameCurrentAction = _currentExecutor != null && _currentExecutor.ActionId == newFirstAction;

            newPlan.CopyTo(_activePlan);
            _activePlanLength = result.PlanLength;
            _currentGoalId = result.ActiveGoal;
            _currentStepIndex = 0;

            if (!isSameCurrentAction)
            {
                StartAction(newFirstAction);
            }
        }

        private void TickActivePlan(float deltaTime)
        {
            if (_currentExecutor == null)
                return;

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
                    {
                        StartAction(_activePlan[_currentStepIndex]);
                    }
                    else
                    {
                        // Цель достигнута — сбрасываем инерцию завершенной цели и ищем новую
                        _currentGoalId = GoalId.None;
                        _replanTimer = 0f;
                    }
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
            _actionCommitmentTimer = _minActionCommitmentTime;
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
            _actionCommitmentTimer = 0f;
        }
    }
}