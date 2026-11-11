using System;
using Instinct.Core.Goap;
using Instinct.Core.State;
using UnityEngine;

namespace Instinct.Runtime.Profiles
{
    [CreateAssetMenu(fileName = "NewInstinctProfile", menuName = "Instinct AI/Agent Profile")]
    public sealed class InstinctProfileSO : ScriptableObject
    {
        [Header("Иерархия наследования")]
        [SerializeField] private InstinctProfileSO? _parentProfile;

        [Header("Негативные особенности архетипа (Фильтры)")]
        [Tooltip("Удаляет любые Consideration с этими ключами из всех унаследованных целей и действий")]
        [SerializeField] private StatKey[] _globalIgnoredStats = Array.Empty<StatKey>();

        [Tooltip("Цели родителя, которые полностью отключаются у этого архетипа")]
        [SerializeField] private GoalId[] _removedGoals = Array.Empty<GoalId>();

        [Tooltip("Действия родителя, которые полностью отключаются у этого архетипа")]
        [SerializeField] private ActionId[] _removedActions = Array.Empty<ActionId>();

        [Header("Конфигурация GOAP + Utility")]
        [SerializeField] private GoalConfig[] _goals = Array.Empty<GoalConfig>();
        [SerializeField] private ActionConfig[] _actions = Array.Empty<ActionConfig>();

        public InstinctProfileSO? ParentProfile => _parentProfile;
        public ReadOnlySpan<StatKey> GlobalIgnoredStats => _globalIgnoredStats;
        public ReadOnlySpan<GoalId> RemovedGoals => _removedGoals;
        public ReadOnlySpan<ActionId> RemovedActions => _removedActions;
        public ReadOnlySpan<GoalConfig> Goals => _goals;
        public ReadOnlySpan<ActionConfig> Actions => _actions;
    }
}