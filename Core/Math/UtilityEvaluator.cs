using System;
using System.Runtime.CompilerServices;
using Instinct.Core.State;

namespace Instinct.Core.Math
{
    // TIP: Интерфейс нужен только для generic where T : struct, IStatProvider.
    public interface IStatProvider
    {
        float GetStat(StatKey key);
    }

    public static class UtilityEvaluator
    {
        private const float ZeroThreshold = 0.0001f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Evaluate(
            ReadOnlySpan<Consideration> considerations,
            ReadOnlySpan<float> blackboardStats,
            float baseWeight = 1f)
        {
            int count = considerations.Length;
            if (count == 0) return 0f;

            float modificationFactor = 1f - (1f / count);
            float totalScore = 1f;

            for (int i = 0; i < count; i++)
            {
                ref readonly Consideration c = ref considerations[i];
                float rawValue = blackboardStats[(int)c.ParameterKey];
                float score = c.Evaluate(rawValue);

                if (score < ZeroThreshold) return 0f;

                float makeUpValue = (1f - score) * modificationFactor;
                totalScore *= score + (makeUpValue * score);
            }

            return totalScore * baseWeight;
        }
    }
}