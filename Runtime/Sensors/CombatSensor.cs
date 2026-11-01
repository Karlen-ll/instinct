using Instinct.Core.Memory;
using Instinct.Core.State;
using UnityEngine;

namespace Instinct.Runtime.Sensors
{
    public sealed class CombatSensor : MonoBehaviour, ISensor
    {
        [SerializeField] private Transform _agentTransform = null!;
        [SerializeField] private Transform _targetTransform = null!;
        [SerializeField] private LayerMask _obstacleMask;

        public float CurrentHealth = 100f;
        public int CurrentAmmo = 30;

        public void CollectData(AgentBlackboard blackboard)
        {
            blackboard.SetStat(StatKey.SelfHealth, CurrentHealth);
            blackboard.SetStat(StatKey.AmmoCount, CurrentAmmo);

            Vector3 toTarget = _targetTransform.position - _agentTransform.position;
            blackboard.SetStat(StatKey.TargetDistance, toTarget.magnitude);
            blackboard.SetFlag(WorldFlag.HasAmmo, CurrentAmmo > 0);

            // TIP: Linecast без аллокаций RaycastHit[]
            bool hasLineOfSight = !Physics.Linecast(
                _agentTransform.position,
                _targetTransform.position,
                _obstacleMask);

            blackboard.SetFlag(WorldFlag.IsTargetVisible, hasLineOfSight);
        }
    }
}