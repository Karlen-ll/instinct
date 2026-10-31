using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Instinct.Core.Math;
using Instinct.Core.State;

namespace Instinct.Core.Goap
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct InstinctAction
    {
        public readonly ActionId Id;
        public readonly float BaseCost;
        public readonly WorldStateMask Preconditions;
        public readonly WorldStateMask Effects;
        public readonly ConsiderationSlice CostModifiers;

        public InstinctAction(
            ActionId id,
            float baseCost,
            WorldStateMask preconditions,
            WorldStateMask effects,
            ConsiderationSlice costModifiers = default)
        {
            Id = id;
            BaseCost = baseCost;
            Preconditions = preconditions;
            Effects = effects;
            CostModifiers = costModifiers;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float CalculateProceduralCost(
            ReadOnlySpan<Consideration> considerationPool,
            ReadOnlySpan<float> blackboardStats)
        {
            if (CostModifiers.Count == 0) return BaseCost;

            ReadOnlySpan<Consideration> slice = CostModifiers.Resolve(considerationPool);
            float utility = UtilityEvaluator.Evaluate(slice, blackboardStats);

            if (utility <= 0.0001f) return float.PositiveInfinity;
            return BaseCost / utility;
        }
    }
}