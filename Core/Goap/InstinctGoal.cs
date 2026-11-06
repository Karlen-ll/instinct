using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Instinct.Core.Math;
using Instinct.Core.State;

namespace Instinct.Core.Goap
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct InstinctGoal
    {
        public readonly GoalId Id;
        public readonly float BaseWeight;
        public readonly float CommitmentBonus;
        public readonly WorldStateMask DesiredState;
        public readonly ConsiderationSlice Considerations;

        public InstinctGoal(
            GoalId id,
            WorldStateMask desiredState,
            ConsiderationSlice considerations,
            float baseWeight = 1f,
            float commitmentBonus = 0.15f)
        {
            Id = id;
            DesiredState = desiredState;
            Considerations = considerations;
            BaseWeight = baseWeight;
            CommitmentBonus = commitmentBonus;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float CalculateUtility(
            ReadOnlySpan<Consideration> considerationPool,
            ReadOnlySpan<float> blackboardStats,
            GoalId currentActiveGoal = GoalId.None)
        {
            ReadOnlySpan<Consideration> slice = Considerations.Resolve(considerationPool);
            float utility = UtilityEvaluator.Evaluate(slice, blackboardStats, BaseWeight);

            // TIP: Если сработало вето (utility == 0), инерция игнорируется — цель удерживать нельзя
            if (utility > 0f && Id == currentActiveGoal)
            {
                utility += CommitmentBonus;
            }

            return utility;
        }
    }
}