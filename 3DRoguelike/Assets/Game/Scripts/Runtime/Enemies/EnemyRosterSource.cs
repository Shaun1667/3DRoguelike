using UnityEngine;

namespace Game
{
    /// <summary>층 · 월드가 아닌 씬(시험 방)에서 디버그 창이 적을 소환할 때 쓸 구성 표를 알려 준다. Setup이 시험 방에 둔다.</summary>
    public sealed class EnemyRosterSource : MonoBehaviour
    {
        [SerializeField] private EnemyRoster roster;

        public EnemyRoster Roster => roster;
    }
}
