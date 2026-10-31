using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Instinct.Core.Math;

namespace Instinct.Core.Goap
{
    /// <summary>
    /// Дескриптор среза в общем пуле Consideration (Burst-совместимый, 3 байта).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ConsiderationSlice
    {
        public readonly ushort StartIndex;
        public readonly byte Count;

        public ConsiderationSlice(ushort startIndex, byte count)
        {
            StartIndex = startIndex;
            Count = count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<Consideration> Resolve(ReadOnlySpan<Consideration> pool)
            => pool.Slice(StartIndex, Count);
    }
}