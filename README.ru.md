# Instinct AI Engine

## Переводы

[English](README.md), [Русский](README.ru.md)

---
**Instinct** — высокопроизводительный Data-Oriented гибридный ИИ-движок для Unity и Godot на C#, объединяющий **Utility AI**
(оценка полезности через аналитические кривые отклика) и **Регрессивный GOAP**
(поиск пути по графу действий от цели к текущему состоянию мира через побитовый $A^*$).

### Ключевые особенности
* **Zero Allocation (Без сборки мусора)**: Построен на `readonly struct`, `ref struct`, `ReadOnlySpan<T>` и `stackalloc`. 0 байт аллокаций в управляемой куче во время работы планировщика.
* **Побитовые состояния мира (ulong)**: До 64 флагов состояния мира упакованы в одно 64-битное целое. Проверка условий, наложение эффектов и расчет эвристики Хэмминга (`BitOperations.PopCount`) выполняются за 1 такт CPU.
* **Многопоточность через Unity Jobs & Burst**: `GoapPlanningJob` (`IJobParallelFor`) параллельно обсчитывает десятки агентов на воркер-потоках, выделяя память под граф $A^*$ прямо на стеке потока (`stackalloc`).
* **Полная отвязка от движка (Decoupling)**: Сборка `Instinct.Core` изолирована флагом `"noEngineReferences"`: true и не зависит от MonoBehaviour, NavMesh или Animator.
* **Иерархический Blackboard**: Мгновенная синхронизация памяти роя/отряда (`SquadBlackboard`) с локальной памятью юнита (`AgentBlackboard`) через линейное копирование памяти (`Span.CopyTo`).

### Структура проекта
```text
Instinct/
├── Core/                        # Чистый C# (noEngineReferences: true)
│   ├── Math/                    # ResponseCurve, Consideration, UtilityEvaluator (компенсация IAUS)
│   ├── State/                   # StatKey, WorldFlag, WorldStateMask (64-битная маска и значения)
│   ├── Memory/                  # AgentBlackboard, SquadBlackboard
│   └── Goap/                    # GoapPlanner (регрессивный A*), InstinctGoal, InstinctAction, PlanNode
└── Runtime/                     # Интеграция с Unity (Burst, Jobs, MonoBehaviour)
    ├── Jobs/                    # GoapPlanningJob, InstinctJobManager
    ├── Sensors/                 # ISensor, CombatSensor
    ├── Execution/               # IActionExecutor, TakeCoverExecutor
    ├── InstinctAgent.cs         # FSM для каждого юнита и мост к очереди Job
    └── InstinctBootstrapper.cs  # База данных сцены и инициализатор отряда
```

## Быстрый старт
- Добавьте компоненты `InstinctJobManager` и `InstinctBootstrapper` на пустой объект сцены.
- Повесьте на префаб бота `InstinctAgent`, сенсоры (`ISensor`) и исполнители действий (`IActionExecutor`).
- Передайте ссылки на ботов в массив _`squadAgents` внутри `InstinctBootstrapper` и запустите сцену.