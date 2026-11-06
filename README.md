# Instinct AI Engine

## Languages

[English](README.md), [Русский](README.ru.md)

---

**Instinct** is a high-performance, data-oriented hybrid AI engine for Unity and Godot (C#), combining **Utility AI**
(continuous priority evaluation via analytical response curves) and **Regressive GOAP**
(Goal-Oriented Action Planning via backward bitwise $A^*$ search).

### Key Features
* **Zero Allocation (0 GC)**: Built entirely on `readonly struct`, `ref struct`, `ReadOnlySpan<T>`, and `stackalloc`. No heap allocations during planning ticks.
* **Bitwise World State (`ulong`)**: Up to 64 world flags packed into a single 64-bit integer. Precondition checks, state transitions, and Hamming distance heuristics (`BitOperations.PopCount`) execute in a single CPU instruction.
* **Burst & Unity Jobs Ready**: `GoapPlanningJob` (`IJobParallelFor`) runs regressive $A^*$ across worker threads using thread-local stack memory (`stackalloc` workspace) with SIMD optimizations (`FloatMode.Fast`).
* **Strict Engine Decoupling**: `Instinct.Core` has `"noEngineReferences": true` and contains pure math and logic, making it portable across Unity, Godot, or standalone .NET servers.
* **Hierarchical Memory**: Fast block-copy synchronization (`Span.CopyTo`) between shared `SquadBlackboard` and per-unit `AgentBlackboard`.

### Project Structure
```text
Instinct/
├── Core/                        # Pure C# (noEngineReferences: true)
│   ├── Math/                    # ResponseCurve, Consideration, UtilityEvaluator (IAUS compensation)
│   ├── State/                   # StatKey, WorldFlag, WorldStateMask (64-bit mask & values)
│   ├── Memory/                  # AgentBlackboard, SquadBlackboard
│   └── Goap/                    # GoapPlanner (Regressive A*), InstinctGoal, InstinctAction, PlanNode
└── Runtime/                     # Unity integration (Burst, Jobs, MonoBehaviour)
    ├── Jobs/                    # GoapPlanningJob, InstinctJobManager
    ├── Sensors/                 # ISensor, CombatSensor
    ├── Execution/               # IActionExecutor, TakeCoverExecutor
    ├── InstinctAgent.cs         # Per-unit FSM & Job queue bridge
    └── InstinctBootstrapper.cs  # Scene database & squad initializer
```

## Quick Start
- Add `InstinctJobManager` and `InstinctBootstrapper` to a scene object.
- Attach `InstinctAgent`, `CombatSensor`, and action executors (`TakeCoverExecutor`) to your NPC prefabs.
- Reference your NPCs in `InstinctBootstrapper._squadAgents` and press Play.