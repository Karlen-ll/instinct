using System;
using System.Runtime.CompilerServices;
using Instinct.Core.Math;
using Instinct.Core.State;

namespace Instinct.Core.Memory
{
    public sealed class AgentBlackboard : IStatProvider
    {
        private const int SquadOffset = (int)StatKey.SquadOffset;
        private readonly float[] _stats = new float[(int)StatKey.Count];

        public ulong LocalWorldState;

        // Вся память (локальная + синхронизированный срез отряда) одним непрерывным куском
        public ReadOnlySpan<float> AllStats => _stats;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetStat(StatKey key, float value) => _stats[(int)key] = value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetStat(StatKey key) => _stats[(int)key];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetFlag(WorldFlag flag, bool value)
        {
            ulong bit = 1UL << (int)flag;
            LocalWorldState = value ? (LocalWorldState | bit) : (LocalWorldState & ~bit);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SyncFromSquad(SquadBlackboard squad, ulong sharedFlagsMask)
        {
            squad.AsSpan().CopyTo(_stats.AsSpan(SquadOffset));
            LocalWorldState = (LocalWorldState & ~sharedFlagsMask) | (squad.SharedWorldState & sharedFlagsMask);
        }
    }
}