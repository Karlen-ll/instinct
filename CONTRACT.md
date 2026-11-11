# Instinct AI Engine — Technical & AI Context Contract

> **Purpose:** This document defines the strict architectural invariants, memory layouts, mathematical formulas,
> and execution contracts of the **Instinct** engine. Both human developers and AI coding assistants MUST adhere to these rules when modifying or extending the codebase.

---

## 1. Core Architectural Invariants

1. **Engine Independence (`Instinct.Core`):**
   * `Instinct.Core.asmdef` enforces `"noEngineReferences": true`.
   * NEVER reference `UnityEngine`, `Godot`, `MonoBehaviour`, or `ScriptableObject` inside `Instinct.Core`.
2. **Zero Heap Allocations in Hot Paths:**
   * Planning, utility evaluation, and blackboard reads MUST allocate `0 bytes` on the managed heap.
   * NEVER use `class`, `List<T>`, `Dictionary<K,V>`, `HashSet<T>`, LINQ, delegates/lambdas, boxing, or `string` inside `Instinct.Core.Goap` or `Instinct.Core.Math`.
3. **Blittable & Burst-Compatible Types:**
   * All structs passed into `GoapPlanningJob` (`InstinctGoal`, `InstinctAction`, `Consideration`, `ResponseCurve`, `ConsiderationSlice`, `WorldStateMask`, `PlanNode`, `PlanResult`, `AgentJobInput`) MUST remain unmanaged/blittable and marked with `[StructLayout(LayoutKind.Sequential)]`.
   * Computed properties inside non-readonly structs (e.g., `PlanNode.FCost`) MUST be marked `readonly` (`public readonly float FCost => GCost + HCost;`) to prevent C# compiler defensive copies on `ref readonly` access.
   * Arrays inside structs are forbidden. Use flat pools (`NativeArray<Consideration>`) addressed via `ConsiderationSlice` (`ushort StartIndex`, `byte Count`).

---

## 2. Binary Data Contracts & Memory Layout

### 2.1. `StatKey` & Blackboard Memory Map (`Instinct.Core.State` / `Instinct.Core.Memory`)
`AgentBlackboard` stores all float parameters in a single contiguous array `float[(int)StatKey.Count]` indexed directly via `(int)StatKey` ($O(1)$ lookup):
* **Indices `0 .. SquadOffset - 1`:** Local unit stats (`SelfHealth = 0`, `SelfStamina = 1`, `TargetDistance = 2`, `AmmoCount = 3`).
* **Indices `SquadOffset .. Count - 1`:** Shared squad stats (`SquadMorale = 4`, `SquadAverageHealth = 5`, `GlobalThreatLevel = 6`).
* **`StatKey.Count = 7`:** Sentinel value defining total array length.
* **Synchronization Contract:** `SquadBlackboard` holds `float[Count - SquadOffset]`. Before planning, `AgentBlackboard.SyncFromSquad` copies the squad span into `_stats.AsSpan(SquadOffset)` via `memmove` and merges shared world flags:

```csharp
  LocalWorldState = (LocalWorldState & ~sharedFlagsMask) | (squad.SharedWorldState & sharedFlagsMask);
```

### 2.2. `WorldFlag` & `WorldStateMask` (`Instinct.Core.State`)

World states are represented as 64-bit unsigned integers (`ulong`), supporting up to 64 boolean flags (`WorldFlag : byte` from `0` to `63`, bitmask `1UL << (int)flag`).
`WorldStateMask` (16 bytes) packs both condition masks and target values:

* `ulong Mask`: Which bits are cared about (`1` = active).
* `ulong Values`: Expected or applied bit state (`0` or `1`).
* **O(1) Satisfaction Check:** `(currentWorldState & Mask) == Values`
* **O(1) State Transition (Apply):** `(currentWorldState & ~Mask) | (Values & Mask)`

---

## 3. Mathematical & Algorithmic Contract

### 3.1. Utility AI (`Instinct.Core.Math`)

1. **Normalization (`Consideration`):**
* Precomputes `_invRange = 1f / (max - min)` in constructor.
* Runtime normalization: `Clamp01((rawValue - Min) * _invRange)`.


2. **Analytical Response Curves (`ResponseCurve`):**
* Evaluates normalized $x \in [0, 1]$ using `System.MathF` (`Linear`, `Polynomial`, `Logistic`, `Logit`).
* `Polynomial` guards against negative base fractional powers (`NaN`) via `MathF.Max(0f, x - XShift)`.
* Output is clamped to `[0, 1]` and inverted (`1f - y`) if `Invert == true`.


3. **Aggregation & IAUS Compensation (`UtilityEvaluator`):**
* Early-exits with `0f` (Veto) if any consideration score is `< 0.0001f`.
* Applies Dave Mark's Infinite Axis Utility System (IAUS) make-up value so multi-factor goals are not penalized against single-factor goals:



$$\text{modificationFactor} = 1 - \frac{1}{N}$$

$$\text{compensatedScore} = \text{score} + (1 - \text{score}) \times \text{modificationFactor} \times \text{score}$$

### 3.2. Hybrid GOAP, Goal Hysteresis & Regressive $A^*$ (`Instinct.Core.Goap`)

1. **Goal Selection & Hysteresis (`GoapPlanner.SelectGoalAndPlan`):**
* Evaluates all unsatisfied `InstinctGoal` entries via `UtilityEvaluator`.
* **Hysteresis (`CommitmentBonus`):** If `goal.Id == baseRequest.CurrentGoal` AND `utility > 0f`, adds `goal.CommitmentBonus` to `utility` to prevent goal dithering. If a consideration vetoed the goal (`utility == 0f`), the bonus is NOT applied.
* **Bitwise Fallback Loop:** Attempts `TryBuildPlan` on the highest-utility goal. If `NoPlanFound`, marks the goal index bit in `ulong rejectedGoalsMask` and retries with the next highest-utility goal.


2. **Procedural Action Cost (`InstinctAction.CalculateProceduralCost`):**
* Evaluated once per action before $A^*$ starts and cached in `workspace.ActionCosts`.
* If `CostModifiers.Count > 0`, computes `BaseCost / utility`. If `utility <= 0.0001f`, returns `float.PositiveInfinity` (vetoing the action from $A^*$ expansion).


3. **Regressive Search (`GoapPlanner.TryBuildPlan`):**
* Searches backward from `rootWorldState = GoalState.ApplyTo(StartState)` toward `StartState`.
* **Unmet Conditions:** `ulong unmetMask = currentNode.WorldState ^ request.StartState;`
* **Action Relevance Filter:**
1. `(action.Effects.Mask & unmetMask) != 0UL`
2. `((currentNode.WorldState ^ action.Effects.Values) & relevantEffectsMask) == 0UL`

* **Regressive State Step (State Before Action):**

```csharp
ulong stateBeforeEffects = (currentNode.WorldState & ~action.Effects.Mask) | (request.StartState & action.Effects.Mask);
ulong nextWorldState = action.Preconditions.ApplyTo(stateBeforeEffects);
```

* **Hamming Heuristic (`HCost`):** `BitOperations.PopCount(nodeState ^ startState)` (compiles to hardware `POPCNT`).
* **Path Reconstruction:** Because search is regressive, terminal node (`WorldState == StartState`) represents the first action to execute. Following `ParentIndex` to `RootParentIndex` (`ushort.MaxValue`) writes `ActionId` directly in chronological execution order (`outPlanBuffer[0..PlanLength-1]`).

---

## 4. Multithreaded Runtime Lifecycle & Action Commitment (`Instinct.Runtime`)

Frame execution order is strictly coordinated via `[DefaultExecutionOrder]`:

1. **`InstinctBootstrapper` (`Order -100`):** Bakes `InstinctProfileSO` via `ProfileMerger.Bake`, initializes shared `NativeArray` database in `InstinctJobManager`, and initializes `SquadBlackboard` on `Start()`.
2. **`InstinctAgent.Update` (`Order 0`):**
* Decrements `_replanTimer` (time-sliced) and `_actionCommitmentTimer`.
* **Action Commitment Guard (`CanInterruptCurrentAction`):** Checks `_currentExecutor == null || (_currentExecutor.IsInterruptible && _actionCommitmentTimer <= 0f)`. If `false`, the agent skips enqueueing to `InstinctJobManager`, saving worker thread capacity.
* When timer expires (or `_currentExecutor == null`) and interruption is allowed, sets `_isWaitingForPlan = true` and calls `InstinctJobManager.Instance.EnqueueAgent(this)`.
* Ticks current `IActionExecutor.OnTick(blackboard, dt)`. When the final step succeeds, resets `_currentGoalId = GoalId.None` to clear goal hysteresis.


3. **`InstinctJobManager.Update` (`Order 50`):**
* Takes up to `MaxBatchSize` (128) agents from `_pendingAgents`.
* Calls `agent.PrepareSensorsAndSync()` on the main thread (runs `ISensor.CollectData` and `SyncFromSquad`).
* Packs agent states (`StartWorldState`, `CurrentGoal`, `MaxDepth`) into `NativeArray<AgentJobInput>` and `NativeArray<float> _flattenedStats`.
* Schedules `GoapPlanningJob` (`IJobParallelFor` with `[BurstCompile]`). Each worker thread allocates its `PlannerWorkspace` on the thread stack via `stackalloc` (~2.3 KB).


4. **`InstinctJobManager.LateUpdate` (`Order 50`):**
* Calls `_activeJobHandle.Complete()`.
* Dispatches `PlanResult` and `ReadOnlySpan<ActionId>` slices back to `agent.ApplyPlanResult(...)`.
* **Seamless Continuation:** If `_currentExecutor.ActionId == newPlan[0]`, the agent updates its plan buffer without interrupting the active executor (`OnExit`/`OnEnter` are skipped).
* Removes processed batch via `_pendingAgents.RemoveRange(0, _scheduledCount)`, preserving any excess agents for the next frame.

---

## 5. Data-Driven Profile Inheritance (`Instinct.Runtime.Profiles`)

1. **Hierarchy Resolution (`ProfileMerger.Bake`):**
* Traverses `InstinctProfileSO.ParentProfile` from root ancestor down to the leaf profile (with cycle detection).
* Outputs a `BakedProfile` containing flat `InstinctGoal[]`, `InstinctAction[]`, and `Consideration[]` arrays linked via `ConsiderationSlice`.


2. **Inheritance Modes (`InheritanceMode`):**
* `Replace`: Overwrites parent Goal/Action definition completely.
* `Merge`: Inherits parent `WorldStateMask` flags and `Consideration` list, overrides `BaseWeight`/`CommitmentBonus`/`BaseCost`, applies `WithFlag` overrides, strips local `IgnoredStats`, and merges/overwrites considerations by `StatKey`.


3. **Negative Traits & Pruning (`IgnoredStats`):**
* `GlobalIgnoredStats` (per-profile) and `IgnoredStats` (per-goal/action) are packed into 64-bit masks (`1UL << (int)StatKey`) to strip unwanted considerations in $O(1)$ bitwise checks.
* **Empty Slice Rule:** If all considerations of an `InstinctGoal` are stripped (`Count == 0`), `ProfileMerger` automatically prunes the goal. If an `InstinctAction` has `0` cost modifiers, it remains valid and uses its constant `BaseCost`.

---

## 6. Extension Checklist (For Developers & AI)

* **Adding a new Numerical Stat:**
1. Add key to `StatKey` enum (`Core/State/StatKey.cs`). Keep local stats below `SquadOffset` and squad stats at or above `SquadOffset`. Increment `Count` (max 64 keys if using bitwise `StatKey` masks in `ProfileMerger`).
2. Populate the stat in an `ISensor` implementation or `SquadBlackboard`.


* **Adding a new World Flag:**
1. Add flag to `WorldFlag` enum (`Core/State/WorldFlag.cs`, max index `63`).
2. Update sensors (`ISensor`) or executors (`IActionExecutor`) to set the flag via `blackboard.SetFlag(...)`.


* **Adding a new Action:**
1. Add ID to `ActionId` enum (`Core/Goap/ActionId.cs`). Ensure `(int)ActionId < InstinctAgent.MaxActionIdCapacity` (or increase `MaxActionIdCapacity`).
2. Implement `IActionExecutor` MonoBehaviour (including `IsInterruptible`) and attach it to the agent prefab.
3. Configure the action in an `InstinctProfileSO` asset (or register the `InstinctAction` struct in the fallback database).
