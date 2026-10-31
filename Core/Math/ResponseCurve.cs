using System;
using System.Runtime.CompilerServices;

namespace Instinct.Core.Math
{
    public enum CurveType : byte
    {
        Linear,
        Polynomial,
        Logistic,
        Logit
    }

    public readonly struct ResponseCurve
    {
        public readonly CurveType Type;
        public readonly float Slope;    // m: наклон
        public readonly float Exponent; // k: степень / крутизна
        public readonly float XShift;   // c: горизонтальный сдвиг
        public readonly float YShift;   // b: вертикальный сдвиг
        public readonly bool Invert;    // инверсия результата (1 - y)

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

        // TIP: AggressiveInlining убирает накладные расходы на вызов метода в горячем цикле планировщика
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Evaluate(float x)
        {
            // TIP: Ожидается, что x уже нормализован в диапазон [0.0, 1.0]
            float y = Type switch
            {
                CurveType.Linear => Slope * (x - XShift) + YShift,
                
                CurveType.Polynomial => Slope * MathF.Pow(x - XShift, Exponent) + YShift,
                
                CurveType.Logistic => Exponent / (1f + MathF.Exp(-10f * Slope * (x - 0.5f - XShift))) + YShift,
                
                CurveType.Logit => InvokeLogit(x),
                
                _ => 0f
            };

            y = Clamp01(y);
            return Invert ? 1f - y : y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float InvokeLogit(float x)
        {
            // TIP: Защита от деления на ноль и логарифма от нуля на границах [0, 1]
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