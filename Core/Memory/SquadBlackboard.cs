using System;
using System.Runtime.CompilerServices;
using Instinct.Core.State;

namespace Instinct.Core.Memory
{
    public sealed class SquadBlackboard
    {
        private const int Offset = (int)StatKey.SquadOffset;
        private const int SquadStatsLength = (int)StatKey.Count - Offset;

        private readonly float[] _squadStats = new float[SquadStatsLength];
        public ulong SharedWorldState;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetStat(StatKey key, float value) => _squadStats[(int)key - Offset] = value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetStat(StatKey key) => _squadStats[(int)key - Offset];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetFlag(WorldFlag flag, bool value)
        {
            ulong bit = 1UL << (int)flag;
            SharedWorldState = value ? (SharedWorldState | bit) : (SharedWorldState & ~bit);
        }

        public ReadOnlySpan<float> AsSpan() => _squadStats;
    }
}