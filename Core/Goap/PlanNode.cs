using System.Runtime.InteropServices;

namespace Instinct.Core.Goap
{
    [StructLayout(LayoutKind.Sequential)]
    public struct PlanNode
    {
        public ulong WorldState;
        public float GCost;
        public float HCost;
        public ushort ParentIndex;
        public ActionId ActionId;

        public readonly float FCost => GCost + HCost;
    }
}