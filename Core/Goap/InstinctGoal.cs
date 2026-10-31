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
        public readonly WorldStateMask DesiredState;
        public readonly ConsiderationSlice Considerations;

        public InstinctGoal(
            GoalId id,
            WorldStateMask desiredState,
            ConsiderationSlice considerations,
            float baseWeight = 1f)
        {
            Id = id;
            DesiredState = desiredState;
            Considerations = considerations;
            BaseWeight = baseWeight;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float CalculateUtility(
            ReadOnlySpan<Consideration> considerationPool,
            ReadOnlySpan<float> blackboardStats)
        {
            ReadOnlySpan<Consideration> slice = Considerations.Resolve(considerationPool);
            return UtilityEvaluator.Evaluate(slice, blackboardStats, BaseWeight);
        }
    }
}