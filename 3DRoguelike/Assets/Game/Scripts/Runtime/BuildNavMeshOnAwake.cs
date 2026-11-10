using Unity.AI.Navigation;
using UnityEngine;

namespace Game
{
    /// <summary>미리 세워 둔 방(시험 방)의 NavMesh를 플레이를 시작할 때 굽는다. 층은 FloorRun이 세운 뒤 직접 굽는다.</summary>
    public sealed class BuildNavMeshOnAwake : MonoBehaviour
    {
        [SerializeField] private NavMeshSurface surface;

        private void Awake()
        {
            surface.BuildNavMesh();
        }
    }
}
