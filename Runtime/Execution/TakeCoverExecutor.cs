using Instinct.Core.Goap;
using Instinct.Core.Memory;
using Instinct.Core.State;
using UnityEngine;
using UnityEngine.AI;

namespace Instinct.Runtime.Execution
{
    public sealed class TakeCoverExecutor : MonoBehaviour, IActionExecutor
    {
        [SerializeField] private NavMeshAgent _navAgent = null!;
        [SerializeField] private Transform _coverPoint = null!;
        [SerializeField] private float _arrivalThreshold = 0.5f;

        public ActionId ActionId => ActionId.TakeCover;

        public void OnEnter(AgentBlackboard blackboard)
        {
            _navAgent.isStopped = false;
            _navAgent.SetDestination(_coverPoint.position);
        }

        public ExecutionStatus OnTick(AgentBlackboard blackboard, float deltaTime)
        {
            if (_navAgent.pathStatus == NavMeshPathStatus.PathInvalid)
                return ExecutionStatus.Failed;

            if (!_navAgent.pathPending && _navAgent.remainingDistance <= _arrivalThreshold)
            {
                blackboard.SetFlag(WorldFlag.IsInCover, true);
                return ExecutionStatus.Success;
            }
            return ExecutionStatus.Running;
        }

        public void OnExit(AgentBlackboard blackboard, bool wasInterrupted)
        {
            if (wasInterrupted && _navAgent.isOnNavMesh)
                _navAgent.isStopped = true;
        }
    }
}