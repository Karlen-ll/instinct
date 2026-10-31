using System;
using Instinct.Core.Math;
using Instinct.Core.State;

namespace Instinct.Core.Goap
{
    public readonly ref struct PlanRequest
    {
        public readonly ulong StartState;
        public readonly WorldStateMask GoalState;
        public readonly ReadOnlySpan<InstinctAction> Actions;
        public readonly ReadOnlySpan<Consideration> ConsiderationPool;
        public readonly ReadOnlySpan<float> BlackboardStats;
        public readonly byte MaxDepth;

        public PlanRequest(
            ulong startState,
            WorldStateMask goalState,
            ReadOnlySpan<InstinctAction> actions,
            ReadOnlySpan<Consideration> considerationPool,
            ReadOnlySpan<float> blackboardStats,
            byte maxDepth = 8)
        {
            StartState = startState;
            GoalState = goalState;
            Actions = actions;
            ConsiderationPool = considerationPool;
            BlackboardStats = blackboardStats;
            MaxDepth = maxDepth;
        }
    }
}