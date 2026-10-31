using Instinct.Core.Memory;

namespace Instinct.Runtime.Sensors
{
    public interface ISensor
    {
        void CollectData(AgentBlackboard blackboard);
    }
}