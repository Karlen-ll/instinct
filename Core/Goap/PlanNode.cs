using System.Runtime.InteropServices;

namespace Instinct.Core.Goap
{
    /// <summary>
    /// Узел графа A* (20 байт). Хранится в Span&lt;PlanNode&gt; на стеке воркер-потока.
    /// FCost — вычисляемое свойство, не занимает места в памяти.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PlanNode
    {
        public ulong WorldState;   // спроецированное состояние мира в этой точке поиска
        public float GCost;        // стоимость от корня (цели) до узла
        public float HCost;        // эвристика Хэмминга до StartState
        public ushort ParentIndex; // индекс родителя в NodePool (RootParentIndex == ushort.MaxValue)
        public ActionId ActionId;  // действие, породившее этот узел (None у корня)

        public float FCost => GCost + HCost;
    }
}