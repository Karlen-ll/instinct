namespace Instinct.Core.Goap
{
    /// <summary>
    /// Результат работы планировщика. byte — для экономии места в NativeArray&lt;PlanResult&gt;.
    /// </summary>
    public enum PlanStatus : byte
    {
        Success = 0,               // План найден, outPlanBuffer заполнен
        GoalAlreadySatisfied = 1,  // Цель уже достигнута StartState — действий не нужно
        NoPlanFound = 2,           // A* исчерпал OpenSet, достижимого пути нет
        BufferTooSmall = 3         // Длина плана > maxDepth или outPlanBuffer.Length
    }
}