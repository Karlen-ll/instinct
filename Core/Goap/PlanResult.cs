using System.Runtime.InteropServices;

namespace Instinct.Core.Goap
{
    /// <summary>
    /// Компактный результат (12 байт). Хранится в NativeArray&lt;PlanResult&gt; внутри GoapPlanningJob.
    /// Сам план лежит отдельно — в FlattenedOutputPlans[agentIndex * PlanStride ...].
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct PlanResult
    {
        public readonly PlanStatus Status;
        public readonly GoalId ActiveGoal;   // какая цель выбрана (GoalId.None при провале)
        public readonly float TotalCost;     // суммарный GCost плана (для отладки/телеметрии)
        public readonly byte PlanLength;     // сколько ActionId валидны в outPlanBuffer

        public PlanResult(PlanStatus status, GoalId activeGoal, float totalCost, int planLength)
        {
            Status = status;
            ActiveGoal = activeGoal;
            TotalCost = totalCost;
            PlanLength = (byte)planLength;
        }
    }
}