using Instinct.Core.Goap;
using Instinct.Core.Math;
using Instinct.Core.Memory;
using Instinct.Core.State;
using Instinct.Runtime.Jobs;
using Instinct.Runtime.Profiles;
using UnityEngine;

namespace Instinct.Runtime
{
    [DefaultExecutionOrder(-100)]
    public sealed class InstinctBootstrapper : MonoBehaviour
    {
        [SerializeField] private InstinctProfileSO? _archetypeProfile;
        [SerializeField] private InstinctAgent[] _squadAgents = null!;

        private SquadBlackboard _squadBlackboard = null!;

        private void Start()
        {
            InitializeDatabase();
            InitializeSquadAndAgents();
        }

        private void InitializeDatabase()
        {
            if (_archetypeProfile != null)
            {
                BakedProfile baked = ProfileMerger.Bake(_archetypeProfile);
                InstinctJobManager.Instance.InitializeSharedDatabase(
                    baked.Goals,
                    baked.Actions,
                    baked.ConsiderationPool);
                return;
            }

            InitializeFallbackDatabase();
        }

        private static void InitializeFallbackDatabase()
        {
            // 1. Плоский пул всех кривых (ConsiderationPool)
            // Индексы жестко привязаны к ConsiderationSlice в целях и действиях
            var considerations = new Consideration[]
            {
                // [0] Для Survive: чем меньше HP (0..100), тем выше паника (квадратичная инвертированная кривая)
                new(
                    StatKey.SelfHealth,
                    min: 0f, max: 100f,
                    new ResponseCurve(CurveType.Polynomial, slope: 1f, exponent: 2f, invert: true)
                ),

                // [1] Для EliminateThreat: чем ближе цель (0..40м), тем выше приоритет атаки
                new(
                    StatKey.TargetDistance,
                    min: 0f, max: 40f,
                    new ResponseCurve(CurveType.Linear, slope: 1f, exponent: 1f, invert: true)
                ),

                // [2] Модификатор стоимости TakeCover: бежать в укрытие выгоднее, когда угроза близко
                new(
                    StatKey.TargetDistance,
                    min: 0f, max: 30f,
                    new ResponseCurve(CurveType.Logistic, slope: 1f, exponent: 1f, invert: true)
                )
            };

            // 2. Декларация целей (InstinctGoal)
            var surviveState = new WorldStateMask().WithFlag(WorldFlag.IsInCover, true);
            var eliminateState = new WorldStateMask().WithFlag(WorldFlag.IsTargetDead, true);

            var goals = new InstinctGoal[]
            {
                new(
                    id: GoalId.Survive,
                    desiredState: surviveState,
                    considerations: new ConsiderationSlice(startIndex: 0, count: 1),
                    baseWeight: 1.2f
                ),
                new(
                    id: GoalId.EliminateThreat,
                    desiredState: eliminateState,
                    considerations: new ConsiderationSlice(startIndex: 1, count: 1),
                    baseWeight: 1.0f
                )
            };

            // 3. Декларация действий (InstinctAction)
            var actions = new InstinctAction[]
            {
                // Занять укрытие: требует наличия укрытия (без прекондишенов), дает IsInCover = true
                new(
                    id: ActionId.TakeCover,
                    baseCost: 2.0f,
                    preconditions: default,
                    effects: new WorldStateMask().WithFlag(WorldFlag.IsInCover, true),
                    costModifiers: new ConsiderationSlice(startIndex: 2, count: 1)
                ),

                // Перезарядка: требует оружие, дает патроны
                new(
                    id: ActionId.Reload,
                    baseCost: 1.5f,
                    preconditions: new WorldStateMask().WithFlag(WorldFlag.HasWeapon, true),
                    effects: new WorldStateMask().WithFlag(WorldFlag.HasAmmo, true)
                ),

                // Выстрел: требует оружие, патроны и видимость цели -> убивает цель
                new(
                    id: ActionId.Shoot,
                    baseCost: 1.0f,
                    preconditions: new WorldStateMask()
                        .WithFlag(WorldFlag.HasWeapon, true)
                        .WithFlag(WorldFlag.HasAmmo, true)
                        .WithFlag(WorldFlag.IsTargetVisible, true),
                    effects: new WorldStateMask().WithFlag(WorldFlag.IsTargetDead, true)
                )
            };

            // TIP: В продакшене массивы выше собираются автоматически из ScriptableObject-профилей
            InstinctJobManager.Instance.InitializeSharedDatabase(goals, actions, considerations);
        }

        private void InitializeSquadAndAgents()
        {
            _squadBlackboard = new SquadBlackboard();
            _squadBlackboard.SetStat(StatKey.SquadMorale, 80f);
            _squadBlackboard.SetStat(StatKey.SquadAverageHealth, 100f);
            _squadBlackboard.SetStat(StatKey.GlobalThreatLevel, 50f);

            for (int i = 0; i < _squadAgents.Length; i++)
            {
                InstinctAgent agent = _squadAgents[i];
                if (agent == null) continue;

                // Выдаем стартовые флаги (оружие в руках)
                agent.Blackboard.SetFlag(WorldFlag.HasWeapon, true);
                agent.Initialize(_squadBlackboard);
            }
        }
    }
}