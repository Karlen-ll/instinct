namespace Instinct.Core.State
{
    /// <summary>
    /// Единый контракт ключей параметров мира/бота.
    /// Индексы 0..SquadOffset-1 — локальные параметры агента, копируемые в AgentBlackboard._stats.
    /// Индексы от SquadOffset — параметры отряда, синхронизируемые из SquadBlackboard.
    ///
    /// ⚠️ Значения фиксированы: AgentBlackboard._stats индексируется напрямую через (int)StatKey.
    /// Изменение значений ломает бинарную совместимость Blackboard ↔ Consideration.
    /// </summary>
    public enum StatKey : ushort
    {
        // --- Локальные параметры (0 .. SquadOffset - 1) ---
        SelfHealth = 0,
        SelfStamina = 1,
        TargetDistance = 2,
        AmmoCount = 3,

        // --- Глобальные параметры отряда (начиная со SquadOffset) ---
        SquadOffset = 4,
        SquadMorale = 4,
        SquadAverageHealth = 5,
        GlobalThreatLevel = 6,

        // Технический маркер размера массива — не использовать как реальный ключ
        Count = 7
    }
}