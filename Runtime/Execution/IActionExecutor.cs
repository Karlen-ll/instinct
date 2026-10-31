using Instinct.Core.Goap;
using Instinct.Core.Memory;

namespace Instinct.Runtime.Execution
{
    public enum ExecutionStatus : byte
    {
        Running = 0,
        Success = 1,
        Failed  = 2
    }

    public interface IActionExecutor
    {
        ActionId ActionId { get; }
        void OnEnter(AgentBlackboard blackboard);
        ExecutionStatus OnTick(AgentBlackboard blackboard, float deltaTime);
        void OnExit(AgentBlackboard blackboard, bool wasInterrupted);
    }
}