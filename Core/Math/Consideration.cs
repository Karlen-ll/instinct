using System.Runtime.CompilerServices;
using Instinct.Core.State;

namespace Instinct.Core.Math
{
    public readonly struct Consideration
    {
        public readonly StatKey ParameterKey;
        public readonly float Min;
        private readonly float _invRange;
        public readonly ResponseCurve Curve;

        public Consideration(StatKey parameterKey, float min, float max, in ResponseCurve curve)
        {
            ParameterKey = parameterKey;
            Min = min;
            float range = max - min;
            _invRange = range != 0f ? 1f / range : 0f;
            Curve = curve;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Evaluate(float rawValue)
        {
            float normalized = ResponseCurve.Clamp01((rawValue - Min) * _invRange);
            return Curve.Evaluate(normalized);
        }
    }
}