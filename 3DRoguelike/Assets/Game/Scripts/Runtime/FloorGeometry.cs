using System.Collections.Generic;
using Game.Rules;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 층 배치를 회색 도형으로 세운다. 시작 방 가운데가 원점이고, 격자 y는 월드 z(북쪽).
    /// 벽은 칸 경계마다 하나만 세우고(이웃한 두 방이 같이 쓴다), 문이 있는 경계는 가운데를 비우고 잠금 벽을 둔다.
    /// </summary>
    public static class FloorGeometry
    {
        public struct Materials
        {
            public Material floor;
            public Material startFloor;
            public Material bossFloor;
            public Material wall;
            public Material lockedDoor;
            public Material treasureFloor;
            /// <summary>바닥 · 벽 텍스처 한 장이 덮는 크기(m). 0이면 도형 UV 그대로(회색 기본 재질).</summary>
            public float floorTile;
            public float wallTile;
        }

        /// <summary>문 순서(layout.Doors)대로 잠금 벽. 처음에는 꺼져 있다.</summary>
        public static GameObject[] Build(FloorLayout layout, FloorTuning tuning, Materials materials, Transform parent)
        {
            var doorByEdge = new Dictionary<(int, int, int, int), int>();
            for (int i = 0; i < layout.Doors.Count; i++)
            {
                FloorDoor door = layout.Doors[i];
                doorByEdge[EdgeKey(door.X, door.Y, door.OtherX, door.OtherY)] = i;
            }

            foreach (FloorRoom room in layout.Rooms) BuildFloor(room, layout, tuning, materials, parent);

            var barriers = new GameObject[layout.Doors.Count];
            // 세로 경계: (x-1, y) | (x, y)
            for (int x = 0; x <= layout.Width; x++)
            {
                for (int y = 0; y < layout.Height; y++)
                {
                    BuildEdge(layout, tuning, materials, parent, doorByEdge, barriers, x - 1, y, x, y);
                }
            }
            // 가로 경계: (x, y-1) / (x, y)
            for (int y = 0; y <= layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    BuildEdge(layout, tuning, materials, parent, doorByEdge, barriers, x, y - 1, x, y);
                }
            }
            return barriers;
        }

        /// <summary>칸 가운데의 월드 위치.</summary>
        public static Vector3 CellCenter(FloorLayout layout, FloorTuning tuning, float x, float y)
        {
            (float worldX, float worldZ) = FloorSpace.CellCenter(layout, tuning.cellSize, x, y);
            return new Vector3(worldX, 0f, worldZ);
        }

        /// <summary>방 가운데의 월드 위치(바닥 높이).</summary>
        public static Vector3 RoomCenter(FloorLayout layout, FloorTuning tuning, FloorRoom room)
        {
            (float worldX, float worldZ) = FloorSpace.RoomCenter(layout, tuning.cellSize, room);
            return new Vector3(worldX, 0f, worldZ);
        }

        /// <summary>월드 위치가 있는 칸. 격자 밖이면 false.</summary>
        public static bool TryGetCell(FloorLayout layout, FloorTuning tuning, Vector3 position, out int x, out int y)
        {
            x = Mathf.FloorToInt(position.x / tuning.cellSize + 0.5f) + layout.Width / 2;
            y = Mathf.FloorToInt(position.z / tuning.cellSize + 0.5f) + layout.Height / 2;
            return x >= 0 && y >= 0 && x < layout.Width && y < layout.Height;
        }

        /// <summary>방 벽에서 margin 이상 안쪽인지.</summary>
        public static bool IsWellInside(FloorLayout layout, FloorTuning tuning, FloorRoom room, Vector3 position, float margin)
        {
            Vector3 center = RoomCenter(layout, tuning, room);
            float halfX = room.Width * tuning.cellSize * 0.5f - margin;
            float halfZ = room.Height * tuning.cellSize * 0.5f - margin;
            return Mathf.Abs(position.x - center.x) <= halfX && Mathf.Abs(position.z - center.z) <= halfZ;
        }

        private static void BuildFloor(FloorRoom room, FloorLayout layout, FloorTuning tuning, Materials materials, Transform parent)
        {
            Material material = room.Kind == RoomKind.Start ? materials.startFloor
                : room.Kind == RoomKind.Boss ? materials.bossFloor
                : room.Kind == RoomKind.Treasure && materials.treasureFloor != null ? materials.treasureFloor
                : materials.floor;
            Vector3 center = RoomCenter(layout, tuning, room);
            Vector3 size = new Vector3(room.Width * tuning.cellSize, 1f, room.Height * tuning.cellSize);
            Block(parent, $"Floor_{room.Id}_{room.Kind}", center + Vector3.down * 0.5f, size, material, materials.floorTile);
        }

        private static void BuildEdge(FloorLayout layout, FloorTuning tuning, Materials materials, Transform parent,
            Dictionary<(int, int, int, int), int> doorByEdge, GameObject[] barriers, int ax, int ay, int bx, int by)
        {
            int a = layout.RoomIdAt(ax, ay);
            int b = layout.RoomIdAt(bx, by);
            if (a == b) return; // 같은 방 안쪽이거나 둘 다 빈칸

            bool vertical = ax != bx;
            Vector3 edgeCenter = (CellCenter(layout, tuning, ax, ay) + CellCenter(layout, tuning, bx, by)) * 0.5f;
            edgeCenter.y = tuning.wallHeight * 0.5f;
            Vector3 along = vertical ? Vector3.forward : Vector3.right;
            string name = $"Wall_{ax},{ay}_{bx},{by}";

            if (!doorByEdge.TryGetValue(EdgeKey(ax, ay, bx, by), out int doorIndex))
            {
                Block(parent, name, edgeCenter, WallSize(vertical, tuning.cellSize + tuning.wallThickness, tuning), materials.wall, materials.wallTile);
                return;
            }

            // 문 양옆 벽. 모서리 쪽 끝만 벽 두께의 절반만큼 늘려 이웃 벽과 틈 없이 만난다.
            float sideLength = (tuning.cellSize - tuning.doorWidth) * 0.5f + tuning.wallThickness * 0.5f;
            float offset = tuning.doorWidth * 0.5f + sideLength * 0.5f;
            Block(parent, name + "_L", edgeCenter - along * offset, WallSize(vertical, sideLength, tuning), materials.wall, materials.wallTile);
            Block(parent, name + "_R", edgeCenter + along * offset, WallSize(vertical, sideLength, tuning), materials.wall, materials.wallTile);

            GameObject barrier = Block(parent, $"Door_{doorIndex}", edgeCenter, WallSize(vertical, tuning.doorWidth, tuning), materials.lockedDoor);
            barrier.SetActive(false);
            barriers[doorIndex] = barrier;
        }

        private static Vector3 WallSize(bool vertical, float length, FloorTuning tuning)
        {
            return vertical
                ? new Vector3(tuning.wallThickness, tuning.wallHeight, length)
                : new Vector3(length, tuning.wallHeight, tuning.wallThickness);
        }

        /// <summary>방향과 무관하게 같은 경계면 같은 키.</summary>
        private static (int, int, int, int) EdgeKey(int ax, int ay, int bx, int by)
        {
            return ax < bx || ax == bx && ay < by ? (ax, ay, bx, by) : (bx, by, ax, ay);
        }

        private static GameObject Block(Transform parent, string name, Vector3 position, Vector3 size, Material material, float tile = 0f)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localScale = size;
            var renderer = block.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            if (tile > 0f) SetTiling(renderer, size, tile);
            return block;
        }
    

        private static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");

        /// <summary>
        /// 테마 재질의 텍스처를 도형 크기에 맞게 되풀이(재질 하나를 같이 쓰므로 렌더러마다 속성 블록). 바닥은 위 면(x · z), 벽은 긴 면(길이 · 높이).
        /// 정규맵 · 금속 맵도 URP Lit에서는 같은 _BaseMap_ST를 쓴다.
        /// </summary>
        private static void SetTiling(Renderer renderer, Vector3 size, float tile)
        {
            bool flat = size.y <= Mathf.Min(size.x, size.z);
            Vector2 repeat = flat
                ? new Vector2(size.x / tile, size.z / tile)
                : new Vector2(Mathf.Max(size.x, size.z) / tile, size.y / tile);
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetVector(BaseMapSt, new Vector4(repeat.x, repeat.y, 0f, 0f));
            renderer.SetPropertyBlock(block);
        }

        /// <summary>
        /// 테마 장식을 정해 둔 자리에 세운다(충돌 상자 그대로 — NavMesh를 굽기 전에 불러야 적이 돌아간다).
        /// 이름 Dressing_{방}_{순번}_{프리팹}. 세운 수를 돌려준다.
        /// </summary>
        public static int BuildDressing(FloorDressing dressing, FloorTheme theme, Transform parent)
        {
            var root = new GameObject("Dressing").transform;
            root.SetParent(parent, false);
            int count = 0;
            for (int roomId = 0; roomId < dressing.RoomCount; roomId++)
            {
                var placements = dressing.For(roomId);
                for (int i = 0; i < placements.Count; i++)
                {
                    DressingPlacement placement = placements[i];
                    FloorTheme.Prop prop = theme.props[placement.Kind];
                    if (prop.prefab == null) continue;
                    GameObject instance = Object.Instantiate(prop.prefab, root);
                    instance.name = $"Dressing_{roomId}_{i}_{prop.prefab.name}";
                    instance.transform.localPosition = new Vector3(placement.X, 0f, placement.Z);
                    instance.transform.localRotation = Quaternion.Euler(0f, placement.Yaw, 0f);
                    instance.transform.localScale = Vector3.one * (prop.scale > 0f ? prop.scale : 1f);
                    count++;
                }
            }
            return count;
        }
}
}
