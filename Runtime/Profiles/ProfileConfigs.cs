using System;
using Instinct.Core.Goap;
using Instinct.Core.Math;
using Instinct.Core.State;
using UnityEngine;

namespace Instinct.Runtime.Profiles
{
    public enum InheritanceMode : byte
    {
        Replace = 0, // Полностью заменяет настройки родительской цели/действия
        Merge = 1    // Наследует факторы и маски родителя, применяя IgnoredStats и добавляя новые
    }

    [Serializable]
    public struct WorldFlagConfig
    {
        public WorldFlag Flag;
        public bool Value;
    }

    [Serializable]
    public struct ResponseCurveConfig
    {
        public CurveType Type;
        public float Slope;
        public float Exponent;
        public float XShift;
        public float YShift;
        public bool Invert;

        public readonly ResponseCurve ToCore()
            => new(Type, Slope, Exponent, XShift, YShift, Invert);
    }

    [Serializable]
    public struct ConsiderationConfig
    {
        public StatKey ParameterKey;
        public float Min;
        public float Max;
        public ResponseCurveConfig Curve;

        public readonly Consideration ToCore()
            => new(ParameterKey, Min, Max, Curve.ToCore());
    }

    [Serializable]
    public struct GoalConfig
    {
        public GoalId Id;
        public InheritanceMode Mode;

        [Min(0.01f)] public float BaseWeight;
        [Min(0f)] public float CommitmentBonus;

        public WorldFlagConfig[] DesiredState;

        [Tooltip("Локальные негативные особенности: удаляет указанные StatKey из унаследованной цели")]
        public StatKey[] IgnoredStats;

        [Tooltip("В режиме Merge добавляет новые факторы или перезаписывает факторы с тем же StatKey")]
        public ConsiderationConfig[] Considerations;
    }

    [Serializable]
    public struct ActionConfig
    {
        public ActionId Id;
        public InheritanceMode Mode;

        [Min(0.01f)] public float BaseCost;

        public WorldFlagConfig[] Preconditions;
        public WorldFlagConfig[] Effects;

        [Tooltip("Локальные негативные особенности: удаляет указанные StatKey из модификаторов стоимости")]
        public StatKey[] IgnoredStats;

        [Tooltip("Модификаторы процедурной стоимости (Utility-кривые)")]
        public ConsiderationConfig[] CostModifiers;
    }
}