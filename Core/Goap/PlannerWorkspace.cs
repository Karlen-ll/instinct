using System;
using System.Runtime.CompilerServices;

namespace Instinct.Core.Goap
{
    /// <summary>
    /// Стековое рабочее пространство планировщика (Zero-Allocation, Burst-compatible).
    /// </summary>
    public ref struct PlannerWorkspace
    {
        public Span<PlanNode> NodePool;
        public Span<ushort> OpenSet;
        public Span<ulong> ClosedStates;
        public Span<float> ActionCosts;

        public int NodeCount;
        public int OpenSetCount;
        public int ClosedCount;

        public PlannerWorkspace(
            Span<PlanNode> nodePool,
            Span<ushort> openSet,
            Span<ulong> closedStates,
            Span<float> actionCosts)
        {
            NodePool = nodePool;
            OpenSet = openSet;
            ClosedStates = closedStates;
            ActionCosts = actionCosts;
            NodeCount = 0;
            OpenSetCount = 0;
            ClosedCount = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            NodeCount = 0;
            OpenSetCount = 0;
            ClosedCount = 0;
        }
    }
}