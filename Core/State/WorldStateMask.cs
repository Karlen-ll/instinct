using System.Runtime.CompilerServices;

namespace Instinct.Core.State
{
    public readonly struct WorldStateMask
    {
        public readonly ulong Mask;
        public readonly ulong Values;

        public WorldStateMask(ulong mask, ulong values)
        {
            Mask = mask;
            Values = values;
        }

        public WorldStateMask WithFlag(WorldFlag flag, bool value)
        {
            ulong bit = 1UL << (int)flag;
            ulong newMask = Mask | bit;
            ulong newValues = value ? (Values | bit) : (Values & ~bit);
            return new WorldStateMask(newMask, newValues);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSatisfiedBy(ulong currentWorldState)
            => (currentWorldState & Mask) == Values;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ApplyTo(ulong currentWorldState)
            => (currentWorldState & ~Mask) | (Values & Mask);
    }
}