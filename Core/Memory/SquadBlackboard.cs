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

        public ReadOnlySpan<float> AsSpan() => _squadStats;
    }
}