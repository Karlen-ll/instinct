using System.Runtime.InteropServices;

namespace Instinct.Core.Goap
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct PlanResult
    {
        public readonly float TotalCost;
        public readonly GoalId ActiveGoal;
        public readonly PlanStatus Status;
        public readonly byte PlanLength;

        public PlanResult(PlanStatus status, GoalId activeGoal, float totalCost, int planLength)
        {
            Status = status;
            ActiveGoal = activeGoal;
            TotalCost = totalCost;
            PlanLength = (byte)planLength;
        }
    }
}