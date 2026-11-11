using System;
using System.Collections.Generic;
using Instinct.Core.Goap;
using Instinct.Core.Math;
using Instinct.Core.State;

namespace Instinct.Runtime.Profiles
{
    /// <summary>
    /// Запеченный плоский профиль, готовый к загрузке в NativeArray для GoapPlanningJob.
    /// </summary>
    public readonly struct BakedProfile
    {
        public readonly InstinctGoal[] Goals;
        public readonly InstinctAction[] Actions;
        public readonly Consideration[] ConsiderationPool;

        public BakedProfile(
            InstinctGoal[] goals,
            InstinctAction[] actions,
            Consideration[] considerationPool)
        {
            Goals = goals;
            Actions = actions;
            ConsiderationPool = considerationPool;
        }
    }

    public static class ProfileMerger
    {
        private sealed class MutableGoal
        {
            public GoalId Id;
            public float BaseWeight;
            public float CommitmentBonus;
            public WorldStateMask DesiredState;
            public readonly List<Consideration> Considerations = new(8);
        }

        private sealed class MutableAction
        {
            public ActionId Id;
            public float BaseCost;
            public WorldStateMask Preconditions;
            public WorldStateMask Effects;
            public readonly List<Consideration> CostModifiers = new(8);
        }

        /// <summary>
        /// Собирает цепочку наследования ScriptableObject от корня к листу и запекает
        /// результат в плоские blittable-массивы с индексацией через ConsiderationSlice.
        /// </summary>
        public static BakedProfile Bake(InstinctProfileSO leafProfile)
        {
            // 1. Строим цепочку от корневого родителя к дочернему профилю (с защитой от циклов)
            var hierarchy = new List<InstinctProfileSO>(4);
            var visited = new HashSet<InstinctProfileSO>();

            InstinctProfileSO? current = leafProfile;
            while (current != null)
            {
                if (!visited.Add(current))
                {
                    throw new InvalidOperationException(
                        $"Обнаружена циклическая ссылка в иерархии профилей: {current.name}");
                }
                hierarchy.Add(current);
                current = current.ParentProfile;
            }
            hierarchy.Reverse();

            var goalMap = new Dictionary<GoalId, MutableGoal>(8);
            var actionMap = new Dictionary<ActionId, MutableAction>(16);
            ulong cumulativeIgnoredStatsMask = 0UL;

            // 2. Пошаговое слияние данных сверху вниз (Root -> Child)
            for (int level = 0; level < hierarchy.Count; level++)
            {
                InstinctProfileSO profile = hierarchy[level];

                // Накапливаем глобальные негативные особенности архетипа
                ulong levelGlobalIgnoreMask = ToBitMask(profile.GlobalIgnoredStats);
                cumulativeIgnoredStatsMask |= levelGlobalIgnoreMask;

                // Удаляем явно отключенные цели и действия родителя
                foreach (GoalId removedGoal in profile.RemovedGoals)
                    goalMap.Remove(removedGoal);

                foreach (ActionId removedAction in profile.RemovedActions)
                    actionMap.Remove(removedAction);

                // Слияние целей
                foreach (ref readonly GoalConfig cfg in profile.Goals)
                {
                    if (cfg.Id == GoalId.None) continue;

                    if (cfg.Mode == InheritanceMode.Replace || !goalMap.TryGetValue(cfg.Id, out MutableGoal? goal))
                    {
                        goal = new MutableGoal
                        {
                            Id = cfg.Id,
                            BaseWeight = cfg.BaseWeight,
                            CommitmentBonus = cfg.CommitmentBonus,
                            DesiredState = ApplyFlags(default, cfg.DesiredState)
                        };
                        goalMap[cfg.Id] = goal;
                    }
                    else
                    {
                        // Режим Merge: обновляем веса и дописываем/переопределяем флаги
                        goal.BaseWeight = cfg.BaseWeight;
                        goal.CommitmentBonus = cfg.CommitmentBonus;
                        goal.DesiredState = ApplyFlags(goal.DesiredState, cfg.DesiredState);

                        // Удаляем локальные IgnoredStats этой цели
                        RemoveIgnoredConsiderations(goal.Considerations, ToBitMask(cfg.IgnoredStats));
                    }

                    MergeConsiderations(goal.Considerations, cfg.Considerations);
                }

                // Слияние действий
                foreach (ref readonly ActionConfig cfg in profile.Actions)
                {
                    if (cfg.Id == ActionId.None) continue;

                    if (cfg.Mode == InheritanceMode.Replace || !actionMap.TryGetValue(cfg.Id, out MutableAction? action))
                    {
                        action = new MutableAction
                        {
                            Id = cfg.Id,
                            BaseCost = cfg.BaseCost,
                            Preconditions = ApplyFlags(default, cfg.Preconditions),
                            Effects = ApplyFlags(default, cfg.Effects)
                        };
                        actionMap[cfg.Id] = action;
                    }
                    else
                    {
                        action.BaseCost = cfg.BaseCost;
                        action.Preconditions = ApplyFlags(action.Preconditions, cfg.Preconditions);
                        action.Effects = ApplyFlags(action.Effects, cfg.Effects);

                        RemoveIgnoredConsiderations(action.CostModifiers, ToBitMask(cfg.IgnoredStats));
                    }

                    MergeConsiderations(action.CostModifiers, cfg.CostModifiers);
                }

                // Применяем глобальный фильтр негативных особенностей текущего уровня ко всем сущностям
                if (levelGlobalIgnoreMask != 0UL)
                {
                    foreach (MutableGoal g in goalMap.Values)
                        RemoveIgnoredConsiderations(g.Considerations, levelGlobalIgnoreMask);

                    foreach (MutableAction a in actionMap.Values)
                        RemoveIgnoredConsiderations(a.CostModifiers, levelGlobalIgnoreMask);
                }
            }

            // 3. Финальная упаковка в плоские массивы для Burst / Unity Jobs
            var flatConsiderations = new List<Consideration>(32);
            var finalGoals = new List<InstinctGoal>(goalMap.Count);
            var finalActions = new List<InstinctAction>(actionMap.Count);

            foreach (MutableGoal g in goalMap.Values)
            {
                // TIP: Если все факторы цели были вырезаны через IgnoredStats, цель отбрасывается,
                // так как UtilityEvaluator для пустого среза всегда возвращает 0
                if (g.Considerations.Count == 0)
                    continue;

                ConsiderationSlice slice = PackSlice(flatConsiderations, g.Considerations);
                finalGoals.Add(new InstinctGoal(
                    g.Id,
                    g.DesiredState,
                    slice,
                    g.BaseWeight,
                    g.CommitmentBonus));
            }

            foreach (MutableAction a in actionMap.Values)
            {
                // TIP: Для действий пустой срез CostModifiers валиден — действие просто использует константную BaseCost
                ConsiderationSlice slice = PackSlice(flatConsiderations, a.CostModifiers);
                finalActions.Add(new InstinctAction(
                    a.Id,
                    a.BaseCost,
                    a.Preconditions,
                    a.Effects,
                    slice));
            }

            return new BakedProfile(
                finalGoals.ToArray(),
                finalActions.ToArray(),
                flatConsiderations.ToArray());
        }

        private static ConsiderationSlice PackSlice(
            List<Consideration> pool,
            List<Consideration> items)
        {
            if (items.Count == 0)
                return default;

            ushort startIndex = (ushort)pool.Count;
            byte count = (byte)Math.Min(items.Count, byte.MaxValue);

            for (int i = 0; i < count; i++)
            {
                pool.Add(items[i]);
            }

            return new ConsiderationSlice(startIndex, count);
        }

        private static WorldStateMask ApplyFlags(
            WorldStateMask baseMask,
            ReadOnlySpan<WorldFlagConfig> flags)
        {
            WorldStateMask result = baseMask;
            for (int i = 0; i < flags.Length; i++)
            {
                result = result.WithFlag(flags[i].Flag, flags[i].Value);
            }
            return result;
        }

        private static void MergeConsiderations(
            List<Consideration> targetList,
            ReadOnlySpan<ConsiderationConfig> incoming)
        {
            for (int i = 0; i < incoming.Length; i++)
            {
                Consideration coreItem = incoming[i].ToCore();

                // Если фактор с таким же StatKey уже есть — перезаписываем его кривую и границы
                int existingIndex = -1;
                for (int j = 0; j < targetList.Count; j++)
                {
                    if (targetList[j].ParameterKey == coreItem.ParameterKey)
                    {
                        existingIndex = j;
                        break;
                    }
                }

                if (existingIndex != -1)
                    targetList[existingIndex] = coreItem;
                else
                    targetList.Add(coreItem);
            }
        }

        private static void RemoveIgnoredConsiderations(
            List<Consideration> list,
            ulong ignoredStatsMask)
        {
            if (ignoredStatsMask == 0UL) return;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                int keyIndex = (int)list[i].ParameterKey;
                if ((ignoredStatsMask & (1UL << keyIndex)) != 0UL)
                {
                    list.RemoveAt(i);
                }
            }
        }

        private static ulong ToBitMask(ReadOnlySpan<StatKey> keys)
        {
            ulong mask = 0UL;
            for (int i = 0; i < keys.Length; i++)
            {
                mask |= 1UL << (int)keys[i];
            }
            return mask;
        }
    }
}