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
        public readonly GoalId CurrentGoal;
        public readonly byte MaxDepth;

        public PlanRequest(
            ulong startState,
            WorldStateMask goalState,
            ReadOnlySpan<InstinctAction> actions,
            ReadOnlySpan<Consideration> considerationPool,
            ReadOnlySpan<float> blackboardStats,
            GoalId currentGoal = GoalId.None,
            byte maxDepth = 8)
        {
            StartState = startState;
            GoalState = goalState;
            Actions = actions;
            ConsiderationPool = considerationPool;
            BlackboardStats = blackboardStats;
            CurrentGoal = currentGoal;
            MaxDepth = maxDepth;
        }
    }
}