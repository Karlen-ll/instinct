using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Instinct.Core.Math
{
    public enum CurveType : byte
    {
        Linear,
        Polynomial,
        Logistic,
        Logit
    }

    // Sequential по умолчанию, атрибут фиксирует намерение и защищает от случайного Auto.
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ResponseCurve
    {
        public readonly CurveType Type;
        public readonly float Slope;
        public readonly float Exponent;
        public readonly float XShift;
        public readonly float YShift;
        public readonly bool Invert;

        public ResponseCurve(
            CurveType type,
            float slope = 1f,
            float exponent = 1f,
            float xShift = 0f,
            float yShift = 0f,
            bool invert = false)
        {
            Type = type;
            Slope = slope;
            Exponent = exponent;
            XShift = xShift;
            YShift = yShift;
            Invert = invert;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Evaluate(float x)
        {
            float y = Type switch
            {
                CurveType.Linear     => Slope * (x - XShift) + YShift,
                CurveType.Polynomial => Slope * MathF.Pow(MathF.Max(0f, x - XShift), Exponent) + YShift,
                CurveType.Logistic   => Exponent / (1f + MathF.Exp(-10f * Slope * (x - 0.5f - XShift))) + YShift,
                CurveType.Logit      => InvokeLogit(x),
                _ => 0f
            };

            y = Clamp01(y);
            return Invert ? 1f - y : y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float InvokeLogit(float x)
        {
            float clampedX = MathF.Max(0.0001f, MathF.Min(0.9999f, x - XShift));
            return Slope * MathF.Log(clampedX / (1f - clampedX)) / 5f + 0.5f + YShift;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}