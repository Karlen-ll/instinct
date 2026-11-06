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

        /// <summary>
        /// Можно ли прервать действие новым планом в текущий момент.
        /// Может меняться динамически (например, false во время каста/выстрела, true при движении).
        /// </summary>
        bool IsInterruptible { get; }

        void OnEnter(AgentBlackboard blackboard);
        ExecutionStatus OnTick(AgentBlackboard blackboard, float deltaTime);
        void OnExit(AgentBlackboard blackboard, bool wasInterrupted);
    }
}