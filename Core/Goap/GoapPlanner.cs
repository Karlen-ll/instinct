using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Instinct.Core.State;

namespace Instinct.Core.Goap
{
    public static class GoapPlanner
    {
        private const ushort RootParentIndex = ushort.MaxValue;

        // =====================================================================
        // Публичный API
        // =====================================================================

        /// <summary>
        /// Выбирает цель с максимальной Utility и строит под неё план.
        /// Регрессивный A*: от GoalState к StartState.
        /// </summary>
        public static PlanResult SelectGoalAndPlan(
            ReadOnlySpan<InstinctGoal> goals,
            in PlanRequest baseRequest,
            ref PlannerWorkspace workspace,
            Span<ActionId> outPlanBuffer)
        {
            int goalsCount = goals.Length;

            // TIP: rejectedGoalsMask — ulong, поэтому жёсткий потолок 64 цели.
            // При G > 64 нужна Span<ulong> или пересмотр архитектуры (обычно 4-8 целей).
            // Assert, а не молчаливое усечение — иначе цель №65 просто исчезнет.
            System.Diagnostics.Debug.Assert(
                goalsCount <= 64,
                $"Instinct.Core: Number of goals ({goalsCount}) exceeds rejectedGoalsMask capacity (64).");

            if (goalsCount > 64)
                goalsCount = 64;

            ulong rejectedGoalsMask = 0UL;
            bool hasAttemptedAnyGoal = false;

            while (true)
            {
                // 1. Найти следующую по полезности НЕотвергнутую и НЕдостигнутую цель
                int bestGoalIndex = -1;
                float highestUtility = 0f;

                for (int i = 0; i < goalsCount; i++)
                {
                    if ((rejectedGoalsMask & (1UL << i)) != 0UL)
                        continue;

                    ref readonly InstinctGoal goal = ref goals[i];

                    // Уже достигнутую цель планировать бессмысленно
                    if (goal.DesiredState.IsSatisfiedBy(baseRequest.StartState))
                        continue;

                    float utility = goal.CalculateUtility(
                        baseRequest.ConsiderationPool,
                        baseRequest.BlackboardStats);

                    if (utility > highestUtility)
                    {
                        highestUtility = utility;
                        bestGoalIndex = i;
                    }
                }

                // 2. Кандидатов не осталось
                if (bestGoalIndex == -1)
                {
                    // TIP: Различаем "всё уже хорошо" и "ничего не достижимо" — статус нужен
                    // для телеметрии и для backoff-логики на стороне InstinctAgent.
                    return hasAttemptedAnyGoal
                        ? new PlanResult(PlanStatus.NoPlanFound, GoalId.None, 0f, 0)
                        : new PlanResult(PlanStatus.GoalAlreadySatisfied, GoalId.None, 0f, 0);
                }

                ref readonly InstinctGoal bestGoal = ref goals[bestGoalIndex];
                hasAttemptedAnyGoal = true;

                var specificRequest = new PlanRequest(
                    baseRequest.StartState,
                    bestGoal.DesiredState,
                    baseRequest.Actions,
                    baseRequest.ConsiderationPool,
                    baseRequest.BlackboardStats,
                    baseRequest.MaxDepth);

                PlanResult result = TryBuildPlan(
                    in specificRequest, bestGoal.Id, ref workspace, outPlanBuffer);

                // 3. Всё, кроме NoPlanFound, финально:
                //    • Success        — план найден
                //    • BufferTooSmall — смена цели не увеличит буфер, менять её бессмысленно
                //    • GoalAlreadySatisfied — недостижимо здесь (см. фильтр выше), но оставляем
                //      для защиты, если кто-то вызовет TryBuildPlan напрямую
                if (result.Status != PlanStatus.NoPlanFound)
                    return result;

                // 4. Цель недостижима в текущем мире — отвергаем и пробуем следующую
                rejectedGoalsMask |= 1UL << bestGoalIndex;
            }
        }

        /// <summary>
        /// Регрессивный поиск A* от желаемого состояния (GoalState) к текущему (StartState).
        /// Результат пишется в outPlanBuffer в прямом порядке выполнения (шаг 1 → шаг N).
        /// </summary>
        public static PlanResult TryBuildPlan(
            in PlanRequest request,
            GoalId targetGoalId,
            ref PlannerWorkspace workspace,
            Span<ActionId> outPlanBuffer)
        {
            if (request.GoalState.IsSatisfiedBy(request.StartState))
                return new PlanResult(PlanStatus.GoalAlreadySatisfied, targetGoalId, 0f, 0);

            workspace.Clear();

            int actionCount = System.Math.Min(request.Actions.Length, workspace.ActionCosts.Length);
            for (int i = 0; i < actionCount; i++)
            {
                workspace.ActionCosts[i] = request.Actions[i].CalculateProceduralCost(
                    request.ConsiderationPool,
                    request.BlackboardStats);
            }

            // 2. Корневой узел: цель, наложенная на стартовое состояние
            ulong rootWorldState = request.GoalState.ApplyTo(request.StartState);

            workspace.NodePool[0] = new PlanNode
            {
                WorldState = rootWorldState,
                GCost = 0f,
                HCost = CalculateHeuristic(rootWorldState, request.StartState),
                ParentIndex = RootParentIndex,
                ActionId = ActionId.None
            };
            workspace.NodeCount = 1;
            PushToOpenSet(ref workspace, 0);

            // 3. Основной цикл регрессивного A*
            while (workspace.OpenSetCount > 0)
            {
                ushort currentIndex = PopLowestFCostNode(ref workspace);
                ref readonly PlanNode currentNode = ref workspace.NodePool[currentIndex];

                // Путь найден: требования узла полностью совпали со стартовым миром
                if (currentNode.WorldState == request.StartState)
                {
                    return ReconstructPlan(
                        ref workspace,
                        currentIndex,
                        targetGoalId,
                        request.MaxDepth,
                        outPlanBuffer);
                }

                // Защита от циклов (Взять → Положить → Взять …)
                if (!TryAddToClosedSet(ref workspace, currentNode.WorldState))
                    continue;

                // Биты, которые ещё надо «снять» с мира, чтобы дойти до StartState
                ulong unmetMask = currentNode.WorldState ^ request.StartState;

                // 4. Перебор действий
                for (int i = 0; i < actionCount; i++)
                {
                    float actionCost = workspace.ActionCosts[i];

                    // Вето от Utility — действие недоступно в текущем контексте
                    if (float.IsPositiveInfinity(actionCost))
                        continue;

                    ref readonly InstinctAction action = ref request.Actions[i];

                    // Действие релевантно, только если затрагивает недостающие биты
                    ulong relevantEffectsMask = action.Effects.Mask & unmetMask;
                    if (relevantEffectsMask == 0UL)
                        continue;

                    // Эффекты действия должны давать именно требуемые значения битов
                    if (((currentNode.WorldState ^ action.Effects.Values) & relevantEffectsMask) != 0UL)
                        continue;

                    // Шаг назад: восстанавливаем состояние ДО выполнения действия
                    //  а) Сбрасываем биты, которые действие меняет, к значениям StartState
                    //  б) Накладываем Preconditions как новые требования
                    ulong stateBeforeEffects =
                        (currentNode.WorldState & ~action.Effects.Mask) |
                        (request.StartState & action.Effects.Mask);

                    ulong nextWorldState = action.Preconditions.ApplyTo(stateBeforeEffects);

                    if (IsInClosedSet(ref workspace, nextWorldState))
                        continue;

                    float newGCost = currentNode.GCost + actionCost;

                    int existingNodeIndex = FindNodeInOpenSet(ref workspace, nextWorldState);
                    if (existingNodeIndex != -1)
                    {
                        ref PlanNode existingNode = ref workspace.NodePool[existingNodeIndex];
                        if (newGCost < existingNode.GCost)
                        {
                            existingNode.GCost = newGCost;
                            existingNode.ParentIndex = currentIndex;
                            existingNode.ActionId = action.Id;
                        }
                        continue;
                    }

                    if (workspace.NodeCount >= workspace.NodePool.Length)
                        continue;

                    ushort newNodeIndex = (ushort)workspace.NodeCount++;
                    workspace.NodePool[newNodeIndex] = new PlanNode
                    {
                        WorldState = nextWorldState,
                        GCost = newGCost,
                        HCost = CalculateHeuristic(nextWorldState, request.StartState),
                        ParentIndex = currentIndex,
                        ActionId = action.Id
                    };

                    PushToOpenSet(ref workspace, newNodeIndex);
                }
            }

            return new PlanResult(PlanStatus.NoPlanFound, targetGoalId, 0f, 0);
        }

        // =====================================================================
        // Хелперы (Zero-Allocation)
        // =====================================================================

        /// <summary>
        /// Эвристика Хэмминга — компилируется в одну инструкцию POPCNT.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float CalculateHeuristic(ulong nodeState, ulong startState)
            => BitOperations.PopCount(nodeState ^ startState);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void PushToOpenSet(ref PlannerWorkspace workspace, ushort nodeIndex)
            => workspace.OpenSet[workspace.OpenSetCount++] = nodeIndex;

        /// <summary>
        /// Линейный поиск минимального FCost + O(1) удаление через Swap-Back.
        /// // TIP: при OpenSet ≤ 64 линейный проход по ushort (1 кэш-линия) быстрее бинарной кучи.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort PopLowestFCostNode(ref PlannerWorkspace workspace)
        {
            int bestOpenIndex = 0;
            ushort bestNodeIndex = workspace.OpenSet[0];
            float bestFCost = workspace.NodePool[bestNodeIndex].FCost;

            int count = workspace.OpenSetCount;
            for (int i = 1; i < count; i++)
            {
                ushort candidateNodeIndex = workspace.OpenSet[i];
                float candidateFCost = workspace.NodePool[candidateNodeIndex].FCost;
                if (candidateFCost < bestFCost)
                {
                    bestFCost = candidateFCost;
                    bestNodeIndex = candidateNodeIndex;
                    bestOpenIndex = i;
                }
            }

            workspace.OpenSet[bestOpenIndex] = workspace.OpenSet[--workspace.OpenSetCount];
            return bestNodeIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FindNodeInOpenSet(ref PlannerWorkspace workspace, ulong worldState)
        {
            int count = workspace.OpenSetCount;
            for (int i = 0; i < count; i++)
            {
                ushort nodeIndex = workspace.OpenSet[i];
                if (workspace.NodePool[nodeIndex].WorldState == worldState)
                    return nodeIndex;
            }
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsInClosedSet(ref PlannerWorkspace workspace, ulong worldState)
        {
            int count = workspace.ClosedCount;
            for (int i = 0; i < count; i++)
            {
                if (workspace.ClosedStates[i] == worldState)
                    return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryAddToClosedSet(ref PlannerWorkspace workspace, ulong worldState)
        {
            if (IsInClosedSet(ref workspace, worldState))
                return false;

            if (workspace.ClosedCount >= workspace.ClosedStates.Length)
                return false;

            workspace.ClosedStates[workspace.ClosedCount++] = worldState;
            return true;
        }

        /// <summary>
        /// Восстанавливает цепочку действий, идя по ParentIndex от терминального узла к корню.
        /// В регрессивном A* этот проход сразу даёт прямой порядок (1-й шаг → цель).
        /// </summary>
        private static PlanResult ReconstructPlan(
            ref PlannerWorkspace workspace,
            ushort terminalNodeIndex,
            GoalId targetGoalId,
            byte maxDepth,
            Span<ActionId> outPlanBuffer)
        {
            float totalCost = workspace.NodePool[terminalNodeIndex].GCost;
            int maxAllowedSteps = System.Math.Min(maxDepth, outPlanBuffer.Length);

            byte stepCount = 0;
            ushort currentIndex = terminalNodeIndex;

            while (currentIndex != RootParentIndex)
            {
                ref readonly PlanNode node = ref workspace.NodePool[currentIndex];

                // Корневой узел (GoalState) не порождает шага — только цель
                if (node.ParentIndex == RootParentIndex)
                    break;

                if (stepCount >= maxAllowedSteps)
                    return new PlanResult(PlanStatus.BufferTooSmall, targetGoalId, totalCost, 0);

                outPlanBuffer[stepCount++] = node.ActionId;
                currentIndex = node.ParentIndex;
            }

            return new PlanResult(PlanStatus.Success, targetGoalId, totalCost, stepCount);
        }
    }
}